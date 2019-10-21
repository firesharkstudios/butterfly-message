using Butterfly.Util;

using Dict = System.Collections.Generic.Dictionary<string, object>;

namespace Butterfly.Message {
    public static class SimpleEvaluator {
        public static string Evaluate(string text, Dict vars, string sourceFilePath) {
            return vars.Format(text);
        }
    }
}
