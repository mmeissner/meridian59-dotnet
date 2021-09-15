using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian59.Bot.Spell
{
    /// <summary>
    /// Get's an stackable item(reagent) from the storage 
    /// </summary>
    public class BotTaskStorageGet : BotTask
    {
        //Name of reagent to get
        public string Reagent = String.Empty;
        /// <summary>
        /// The Name of the NPC to get from
        /// </summary>
        public string Target = String.Empty;
        /// <summary>
        /// The maximum amount in inventory
        /// </summary>
        public uint Max = 0;
        /// <summary>
        /// The minimum amount in that triggers the get
        /// </summary>
        public uint Min = 0;

        public BotTaskStorageGet()
        {
        }

        public BotTaskStorageGet(string reagent, string target, uint max, uint min)
        {
            Reagent = reagent;
            Target = target;
            Max = max;
            Min = min;
        }
    }
}
