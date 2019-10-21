using System;
using System.Collections.Generic;
using System.IO;

using Dict = System.Collections.Generic.Dictionary<string, object>;

namespace Butterfly.Message {
    public class SendMessageFileParser {

        protected Dictionary<string, Func<string, Dict, string, string>> evaluatorByExtension;

        public SendMessageFileParser(Dictionary<string, Func<string, Dict, string, string>> evaluatorByExtension = null) {
            if (evaluatorByExtension==null) {
                this.evaluatorByExtension = new Dictionary<string, Func<string, Dict, string, string>> {
                    [".liquid"] = ScribanEvaluator.Evaluate,
                    [".txt"] = SimpleEvaluator.Evaluate
                };
            }
            else {
                this.evaluatorByExtension = evaluatorByExtension;
            }
        }

        public SendMessage Parse(string fileName) {
            if (!File.Exists(fileName)) throw new Exception("Could not find file '" + fileName + "'");
            var extension = Path.GetExtension(fileName);
            if (!this.evaluatorByExtension.TryGetValue(extension, out Func<string, Dict, string, string> evaluator)) throw new Exception($"Unknown evaluator extension {extension}");

            string text = File.ReadAllText(fileName);
            return SendMessage.Parse(text, evaluator, Path.GetDirectoryName(fileName));
        }

    }
}
