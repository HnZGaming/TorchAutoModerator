using System.Collections.Generic;
using System.Linq;
using Utils.General;

namespace AutoModerator.Core
{
    public class BlockTypePairCollection
    {
        readonly Dictionary<string, HashSet<string>> _blockTypes;

        public BlockTypePairCollection()
        {
            _blockTypes = new Dictionary<string, HashSet<string>>();
        }

        static bool TryParseBlockTypePair(string input, out string typeId, out string subtypeId)
        {
            typeId = null;
            subtypeId = null;

            var pair = input.Split('/');
            if (!pair.Any()) return false;

            typeId = pair[0];
            if (string.IsNullOrEmpty(typeId)) return false;
            if (string.IsNullOrWhiteSpace(typeId)) return false;

            subtypeId = pair.GetElementAtOrElse(1, "");
            return true;
        }

        public void Clear()
        {
            _blockTypes.Clear();
        }

        public bool TryAdd(string input)
        {
            if (TryParseBlockTypePair(input, out var type, out var subtype))
            {
                _blockTypes.Add(type, subtype);
                return true;
            }

            return false;
        }

        public bool Contains(string type, string subtype)
        {
            // type not present
            if (!_blockTypes.TryGetValue(type, out var subtypes)) return false;

            // all subtypes under the type match
            if (subtypes.Contains("")) return true;

            return subtypes.Contains(subtype);
        }
    }
}