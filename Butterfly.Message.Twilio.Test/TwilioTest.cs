/* Any copyright is dedicated to the Public Domain.
 * http://creativecommons.org/publicdomain/zero/1.0/ */

using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Butterfly.Message.Twilio;
using Butterfly.Message.Test;

namespace Butterfly.Notify.Test {
    [TestClass]
    public class TwilioTest {
        [TestMethod]
        public async Task SendTwilioSmsNotifyMessage() {
            var notifyMessageSender = new TwilioTextMessageSender("my-sid", "my-auth-token");
            await MessageTest.SendPhoneNotifyMessage(notifyMessageSender);
        }
    }
}
