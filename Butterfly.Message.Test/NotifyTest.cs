﻿/* Any copyright is dedicated to the Public Domain.
 * http://creativecommons.org/publicdomain/zero/1.0/ */

using System;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Butterfly.Util;
using Butterfly.Db;

namespace Butterfly.Message.Test {
    [TestClass]
    public class NotifyTest {
        [TestMethod]
        public void ParseNotifyMessage() {
            var email = FileX.LoadResourceAsText(Assembly.GetExecutingAssembly(), "Butterfly.Notify.Test.email.txt");
            var templateNotifyMessage = SendMessage.Parse(email);
            var notifyMessage = templateNotifyMessage.Evaluate(new {
                first_name = "Bob"
            });
            Assert.IsTrue(notifyMessage.bodyText.Trim().StartsWith("Hello, Bob."));
        }

        [TestMethod]
        public static async Task SendEmailNotifyMessage(IMessageSender notifyMessageSender) {
            var database = new Db.Memory.MemoryDatabase();
            await database.CreateFromResourceFileAsync(Assembly.GetExecutingAssembly(), "Butterfly.Notify.Test.db.sql");
            database.SetDefaultValue("id", tableName => Guid.NewGuid().ToString());
            database.SetDefaultValue("created_at", tableName => DateTime.Now);

            var notifyMessageManager = new SendMessageQueueManager(database, emailNotifyMessageSender: notifyMessageSender);
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
            await database.CreateFromResourceFileAsync(Assembly.GetExecutingAssembly(), "Butterfly.Notify.Test.db.sql");
            database.SetDefaultValue("id", tableName => Guid.NewGuid().ToString());
            database.SetDefaultValue("created_at", tableName => DateTime.Now);

            var notifyMessageManager = new SendMessageQueueManager(database, phoneNotifyMessageSender: notifyMessageSender);
            notifyMessageManager.Start();
            var notifyMessage = new SendMessage("+1 316 712 7412", "+1 316 555 1212", null, "Just testing", null);
            using (ITransaction transaction = await database.BeginTransactionAsync()) {
                await notifyMessageManager.Queue(transaction, notifyMessage);
            }
            await Task.Delay(200000);
        }

    }
}
