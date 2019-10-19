/* Any copyright is dedicated to the Public Domain.
 * http://creativecommons.org/publicdomain/zero/1.0/ */

using System;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Butterfly.Db;
using Butterfly.Util;

namespace Butterfly.Message.Test {
    [TestClass]
    public class MessageTest {
        [TestMethod]
        public void ParseSendMessage() {
            var email = FileX.LoadResourceAsText(Assembly.GetExecutingAssembly(), "Butterfly.Message.Test.email.txt");
            var sendMessageTemplate = SendMessage.Parse(email);
            var sendMessage = sendMessageTemplate.Evaluate(new {
                first_name = "Bob"
            });
            Assert.IsTrue(sendMessage.bodyText.Trim().StartsWith("Hello, Bob."));
        }

        [TestMethod]
        public static async Task SendEmailNotifyMessage(IMessageSender notifyMessageSender) {
            var database = new Db.Memory.MemoryDatabase();
            await database.CreateFromResourceFileAsync(Assembly.GetExecutingAssembly(), "Butterfly.Message.Test.db.sql");
            database.SetDefaultValue("id", tableName => Guid.NewGuid().ToString());
            database.SetDefaultValue("created_at", tableName => DateTime.Now);

            var notifyMessageManager = new SendMessageQueueManager(database, emailMessageSender: notifyMessageSender);
            notifyMessageManager.Start();
            var notifyMessage = new SendMessage("kent@fireshark.com", "kent13304@yahoo.com", "Test SES", "Just testing", null);
            using (ITransaction transaction = await database.BeginTransactionAsync()) {
                await notifyMessageManager.Queue(transaction, notifyMessage);
                await transaction.CommitAsync();
            }
            await Task.Delay(200000);
        }

        [TestMethod]
        public static async Task SendPhoneNotifyMessage(IMessageSender notifyMessageSender) {
            var database = new Db.Memory.MemoryDatabase();
            await database.CreateFromResourceFileAsync(Assembly.GetExecutingAssembly(), "Butterfly.Message.Test.db.sql");
            database.SetDefaultValue("id", tableName => Guid.NewGuid().ToString());
            database.SetDefaultValue("created_at", tableName => DateTime.Now);

            var notifyMessageManager = new SendMessageQueueManager(database, textMessageSender: notifyMessageSender);
            notifyMessageManager.Start();
            var notifyMessage = new SendMessage("+1 316 712 7412", "+1 316 555 1212", null, "Just testing", null);
            using (ITransaction transaction = await database.BeginTransactionAsync()) {
                await notifyMessageManager.Queue(transaction, notifyMessage);
            }
            await Task.Delay(200000);
        }

    }
}
