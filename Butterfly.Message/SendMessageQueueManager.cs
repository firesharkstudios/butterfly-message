/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NLog;

using Butterfly.Db;
using Butterfly.Util;
using Butterfly.Util.Field;

using Dict = System.Collections.Generic.Dictionary<string, object>;

namespace Butterfly.Message {

    public enum SendMessageType {
        Email,
        Text,
    }

    public class SendMessageQueueManager {

        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();

        protected readonly IDatabase database;

        protected readonly SendMessageEngine emailSendMessageEngine;
        protected readonly SendMessageEngine textSendMessageEngine;

        protected readonly string sendMessageTableName;
        protected readonly string sendVerifyTableName;
        protected readonly int verifyCodeExpiresSeconds;

        protected readonly SendMessage verifyEmailSendMessage;
        protected readonly SendMessage verifyTextSendMessage;
        protected readonly string verifyCodeFormat;

        protected readonly static EmailFieldValidator EMAIL_FIELD_VALIDATOR = new EmailFieldValidator("email", false, true);
        protected readonly static PhoneFieldValidator PHONE_FIELD_VALIDATOR = new PhoneFieldValidator("phone", false);

        protected readonly static Random RANDOM = new Random();

        public SendMessageQueueManager(IDatabase database, IMessageSender emailMessageSender = null, IMessageSender textMessageSender = null, string sendMessageTableName = "send_message", string sendVerifyTableName = "send_verify", int verifyCodeExpiresSeconds = 3600, SendMessage verifyEmailSendMessage = null, SendMessage verifyTextSendMessage = null, string verifyCodeFormat = "###-###") {
            this.database = database;
            this.emailSendMessageEngine = emailMessageSender == null ? null : new SendMessageEngine(SendMessageType.Email, emailMessageSender, database, sendMessageTableName);
            this.textSendMessageEngine = textMessageSender == null ? null : new SendMessageEngine(SendMessageType.Text, textMessageSender, database, sendMessageTableName);
            this.sendMessageTableName = sendMessageTableName;
            this.sendVerifyTableName = sendVerifyTableName;
            this.verifyCodeExpiresSeconds = verifyCodeExpiresSeconds;

            this.verifyEmailSendMessage = verifyEmailSendMessage;
            this.verifyTextSendMessage = verifyTextSendMessage;
            this.verifyCodeFormat = verifyCodeFormat;
        }

        public void Start() {
            this.emailSendMessageEngine?.Start();
            this.textSendMessageEngine?.Start();
        }

        public void Stop() {
            this.emailSendMessageEngine?.Stop();
            this.textSendMessageEngine?.Stop();
        }

        public async Task SendVerifyCodeAsync(string contact) {
            logger.Debug($"SendVerifyCodeAsync():contact={contact}");

            // Scrub contact
            string scrubbedContact = Validate(contact);
            logger.Debug($"SendVerifyCodeAsync():scrubbedContact={scrubbedContact}");

            // Generate code and expires at
            int digits = this.verifyCodeFormat.Count(x => x=='#');
            int min = (int)Math.Pow(10, digits - 1);
            int max = (int)Math.Pow(10, digits) - 1;
            int code = RANDOM.Next(0, max - min) + min;
            logger.Debug($"SendVerifyCodeAsync():digits={digits},min={min},max={max},code={code}");
            DateTime expiresAt = DateTime.Now.AddSeconds(this.verifyCodeExpiresSeconds);

            // Insert/update database
            string id = await this.database.SelectValueAsync<string>($"SELECT id FROM {this.sendVerifyTableName}", new {
                contact = scrubbedContact
            });

            using (ITransaction transaction = await this.database.BeginTransactionAsync()) {
                if (id == null) {
                    await transaction.InsertAsync<string>(this.sendVerifyTableName, new {
                        contact = scrubbedContact,
                        verify_code = code,
                        expires_at = expiresAt,
                    });
                }
                else {
                    await transaction.UpdateAsync(this.sendVerifyTableName, new {
                        id,
                        verify_code = code,
                        expires_at = expiresAt,
                    });
                }

                // Send message
                var sendMessageType = DetectSendMessageType(scrubbedContact);
                SendMessage sendMessage = null;
                switch (sendMessageType) {
                    case SendMessageType.Email:
                        if (this.verifyEmailSendMessage == null) throw new Exception("Server must be configured with verify email send message");
                        sendMessage = this.verifyEmailSendMessage;
                        break;
                    case SendMessageType.Text:
                        if (this.verifyTextSendMessage == null) throw new Exception("Server must be configured with verify text send message");
                        sendMessage = this.verifyTextSendMessage;
                        break;
                }
                var evaluatedSendMessage = sendMessage.Evaluate(new {
                    contact = scrubbedContact,
                    code = code.ToString(this.verifyCodeFormat)
                });
                await this.Queue(transaction, evaluatedSendMessage);

                await transaction.CommitAsync();
            }
        }

        public async Task<string> VerifyAsync(string contact, int code) {
            logger.Debug($"VerifyAsync():contact={contact},code={code}");
            string scrubbedContact = Validate(contact);
            Dict result = await this.database.SelectRowAsync($"SELECT verify_code, expires_at FROM {this.sendVerifyTableName}", new {
                contact = scrubbedContact
            });
            int verifyCode = result.GetAs("verify_code", -1);
            if (code == -1 || code!=verifyCode) throw new Exception("Invalid contact and/or verify code");

            int expiresAtUnix = result.GetAs("expires_at", -1);
            if (DateTimeX.FromUnixTimestamp(expiresAtUnix) < DateTime.Now) throw new Exception("Expired verify code");

            return scrubbedContact;
        }

        protected static string Validate(string value) {
            logger.Debug($"Validate():value={value}");
            if (value.Contains("@")) {
                return EMAIL_FIELD_VALIDATOR.Validate(value);
            }
            else {
                return PHONE_FIELD_VALIDATOR.Validate(value);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="transaction"></param>
        /// <param name="sendMessages"></param>
        /// <returns></returns>
        public async Task Queue(ITransaction transaction, params SendMessage[] sendMessages) {
            bool emailQueued = false;
            bool phoneTextQueued = false;
            foreach (var sendMessage in sendMessages) {
                if (sendMessage == null) continue;

                if (string.IsNullOrEmpty(sendMessage.from)) throw new Exception("From address cannot be blank");
                string scrubbedFrom = Validate(sendMessage.from);

                if (string.IsNullOrEmpty(sendMessage.to)) throw new Exception("To address cannot be blank");
                string scrubbedTo = Validate(sendMessage.to);

                SendMessageType sendMessageType = this.DetectSendMessageType(scrubbedTo);
                switch (sendMessageType) {
                    case SendMessageType.Email:
                        if (this.emailSendMessageEngine == null) throw new Exception("No email message sender configured");
                        await this.emailSendMessageEngine.Queue(transaction, scrubbedFrom, scrubbedTo, sendMessage.subject, sendMessage.bodyText, sendMessage.bodyHtml, sendMessage.priority, sendMessage.extraData);
                        emailQueued = true;
                        break;
                    case SendMessageType.Text:
                        if (this.textSendMessageEngine == null) throw new Exception("No phone text message sender configured");
                        await this.textSendMessageEngine.Queue(transaction, scrubbedFrom, scrubbedTo, sendMessage.subject, sendMessage.bodyText, sendMessage.bodyHtml, sendMessage.priority, sendMessage.extraData);
                        phoneTextQueued = true;
                        break;
                }
            }

            transaction.OnCommit(() => {
                if (emailQueued) this.emailSendMessageEngine.Pulse();
                if (phoneTextQueued) this.textSendMessageEngine.Pulse();
                return Task.FromResult(0);
            });
        }

        protected SendMessageType DetectSendMessageType(string to) {
            if (string.IsNullOrWhiteSpace(to)) throw new Exception("Invalid contact '" + to + "'");
            else if (to.Contains("@")) return SendMessageType.Email;
            else if (to.StartsWith("+")) return SendMessageType.Text;
            else throw new Exception("Invalid to '" + to + "'");
        }

        protected class SendMessageEngine {
            protected readonly SendMessageType sendMessageType;
            protected readonly IMessageSender sendMessageSender;
            protected readonly IDatabase database;
            protected readonly string sendMessageTableName;

            protected CancellationTokenSource cancellationTokenSource = null;

            public SendMessageEngine(SendMessageType sendMessageType, IMessageSender sendMessageSender, IDatabase database, string sendMessageTableName) {
                this.sendMessageType = sendMessageType;
                this.sendMessageSender = sendMessageSender;
                this.database = database;
                this.sendMessageTableName = sendMessageTableName;
            }

            protected bool started;

            public void Start() {
                if (!this.started) {
                    Task task = this.Run();
                }
            }

            public void Stop() {
                this.started = false;
                this.Pulse();
            }

            public async Task Queue(ITransaction transaction, string from, string to, string subject, string bodyText, string bodyHtml, byte priority, Dict extraData) {
                logger.Debug($"Queue():type={this.sendMessageType},priority={priority},from={from},to={to},subject={subject}");
                string extraJson = extraData != null && extraData.Count > 0 ? JsonUtil.Serialize(extraData) : null;
                await transaction.InsertAsync<string>(this.sendMessageTableName, new {
                    message_type = (byte)this.sendMessageType,
                    message_priority = priority,
                    message_from = from,
                    message_to = to,
                    message_subject = subject,
                    message_body_text = bodyText,
                    message_body_html = bodyHtml,
                    extra_json = extraJson,
                });
            }

            public void Pulse() {
                this.cancellationTokenSource?.Cancel();
            }

            async Task Run() {
                this.started = true;
                while (this.started) {
                    Dict message = await this.database.SelectRowAsync(
                        @"SELECT *
                        FROM " + this.sendMessageTableName + @"
                        WHERE message_type=@messageType AND sent_at IS NULL
                        ORDER BY message_priority DESC, created_at", new {
                            messageType = (byte)this.sendMessageType
                        });
                    if (message == null) {
                        logger.Trace("Run():Waiting indefinitely");
                        try {
                            this.cancellationTokenSource = new CancellationTokenSource();
                            await Task.Delay(60000, cancellationTokenSource.Token);
                        }
                        catch (TaskCanceledException) {
                            this.cancellationTokenSource = null;
                        }
                        logger.Trace("Run():Waking up");
                    }
                    else {
                        string sentMessageId = null;
                        string error = null;
                        try {
                            string from = message.GetAs("message_from", (string)null);
                            string to = message.GetAs("message_to", (string)null);
                            string subject = message.GetAs("message_subject", (string)null);
                            string bodyText = message.GetAs("message_body_text", (string)null);
                            string bodyHtml = message.GetAs("message_body_html", (string)null);
                            logger.Debug($"Run():Sending message to {to}");

                            sentMessageId = await this.sendMessageSender.SendAsync(from, to, subject, bodyText, bodyHtml);
                        }
                        catch (Exception e) {
                            error = e.Message;
                            logger.Error(e);
                        }

                        var id = message.GetAs("id", (string)null);
                        await this.database.UpdateAndCommitAsync(this.sendMessageTableName, new {
                            id,
                            sent_at = DateTime.Now,
                            sent_message_id = sentMessageId,
                            send_error = error,
                        });

                        int totalMillis = (int)(this.sendMessageSender.CanSendNextAt - DateTime.Now).TotalMilliseconds;
                        if (totalMillis>0) {
                            logger.Trace("Run():Sleeping for " + totalMillis + "ms");
                            await Task.Delay(totalMillis);
                        }
                    }

                }
            }
        }
    }
}
