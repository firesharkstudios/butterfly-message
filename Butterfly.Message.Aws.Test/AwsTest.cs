/* Any copyright is dedicated to the Public Domain.
 * http://creativecommons.org/publicdomain/zero/1.0/ */

using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Butterfly.Message.Test;

namespace Butterfly.Message.Aws.Test {
    [TestClass]
    public class AwsTest {
        [TestMethod]
        public async Task SendAwsSesNotifyMessage() {
            var notifyMessageSender = new AwsSesEmailMessageSender();
            await NotifyTest.SendEmailNotifyMessage(notifyMessageSender);
        }
    }
}
