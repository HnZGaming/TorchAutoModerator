using System.Collections.Generic;
using System.Linq;
using Utils.General;
using VRage.Game.ModAPI;

namespace AutoModerator.Core
{
    public class BlockTypeCollection
    {
        // key: type id; value: set of subtypes where, an empty subtype ("") should match all subtypes.
        readonly Dictionary<string, HashSet<string>> _blockTypes;

        public BlockTypeCollection()
        {
            _blockTypes = new Dictionary<string, HashSet<string>>();
        }

        static bool TryParseBlockType(string input, out string type, out string subtype)
        {
            type = null;
            subtype = null;

            var pair = input.Split('/');
            if (!pair.Any()) return false;

            type = pair[0];
            subtype = pair.GetElementAtOrElse(1, "");

            return true;
        }

        public void Clear()
        {
            _blockTypes.Clear();
        }

        public void Add(string blockType)
        {
            if (TryParseBlockType(blockType, out var type, out var subtype))
            {
                _blockTypes.Add(type, subtype);
            }
        }

        public bool ContainsBlockTypeOf(IMyCubeBlock block)
        {
            // type    -- eg. "Thrust"
            // subtype -- eg. "LargeBlockSmallAtmosphericThrust"

            var type = block.BlockDefinition.TypeIdString;

            // type not present
            if (!_blockTypes.TryGetValue(type, out var subtypes)) return false;

            // shouldn't happen but to be sure
            if (!subtypes.Any()) return false;

            // all subtypes under the type match
            if (subtypes.Contains("")) return true;

            var subtype = block.BlockDefinition.SubtypeId;
            return subtypes.Contains(subtype);
        }
    }
}