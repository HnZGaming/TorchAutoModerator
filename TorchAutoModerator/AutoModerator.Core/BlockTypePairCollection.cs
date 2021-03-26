using System.Collections.Generic;
using System.Text.RegularExpressions;
using Utils.General;

namespace AutoModerator.Core
{
    public class BlockTypePairCollection
    {
        static readonly Regex TypePattern = new Regex(@"^\w+$");
        readonly Dictionary<string, HashSet<string>> _blockTypes;

        public BlockTypePairCollection()
        {
            _blockTypes = new Dictionary<string, HashSet<string>>();
        }

        static bool TryParseBlockTypePair(string input, out string typeName, out string subtypeName)
        {
            typeName = null;
            subtypeName = null;

            var pair = input.Split('/');
            if (!pair.TryGetElementAt(0, out typeName)) return false;
            if (!TypePattern.IsMatch(typeName)) return false;

            typeName = $"MyObjectBuilder_{typeName}";
            subtypeName = pair.GetElementAtOrElse(1, "");
            return true;
        }

        public void Clear()
        {
            _blockTypes.Clear();
        }

        public bool TryAdd(string input)
        {
            if (TryParseBlockTypePair(input, out var typeName, out var subtypeName))
            {
                _blockTypes.Add(typeName, subtypeName);
                return true;
            }

            return false;
        }

        public bool Contains(string typeName, string subtypeName)
        {
            // type not present
            if (!_blockTypes.TryGetValue(typeName, out var subtypes)) return false;

            // all subtypes under the type match
            if (subtypes.Contains("")) return true;

            return subtypes.Contains(subtypeName);
        }
    }
}