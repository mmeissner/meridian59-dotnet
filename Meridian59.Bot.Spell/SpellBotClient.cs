﻿/*
 Copyright (c) 2012-2013 Clint Banzhaf
 This file is part of "Meridian59 .NET".

 "Meridian59 .NET" is free software: 
 You can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, 
 either version 3 of the License, or (at your option) any later version.

 "Meridian59 .NET" is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
 without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 See the GNU General Public License for more details.

 You should have received a copy of the GNU General Public License along with "Meridian59 .NET".
 If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Meridian59.Data.Models;
using Meridian59.Data;
using Meridian59.Protocol.GameMessages;
using Meridian59.Protocol.Enums;
using Meridian59.Common.Constants;
using Meridian59.Common.Enums;
using Meridian59.Common;
using Meridian59.Files;

namespace Meridian59.Bot.Spell
{
    /// <summary>
    /// A client which acts as a spell training bot
    /// </summary>
    public class SpellBotClient : BotClient<GameTick, ResourceManager, DataController, SpellBotConfig>
    {
        #region Constants
        protected const double STARTUPSLEEP = 5000.0;
        #endregion

        protected double tickSleepUntil;
        protected BotTask currentTask;
        protected uint imps = 0;

        protected List<BuyRequest> buyRequests = new List<BuyRequest>();
        /// <summary>
        /// Constructor
        /// </summary>
        public SpellBotClient()
            : base()
        {           
        }

        /// <summary>
        /// 
        /// </summary>
        public override void Init()
        {
            base.Init();

            // set initial sleep for startup
            tickSleepUntil = GameTick.Current + STARTUPSLEEP;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Message"></param>
        protected override void HandleGameModeMessage(GameModeMessage Message)
        {
            base.HandleGameModeMessage(Message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Message"></param>
        protected override void HandlePlayerMessage(PlayerMessage Message)
        {
            base.HandlePlayerMessage(Message);
        }
        
        /// <summary>
        /// Handles someone offers you first
        /// </summary>
        /// <param name="Message"></param>
        protected override void HandleOfferMessage(OfferMessage Message)
        {
            base.HandleOfferMessage(Message);

            if (Data.Trade.TradePartner == null)
                return;

            // accept any items from configured admins in config, offer nothing in return
            if (Config.IsAdmin(Data.Trade.TradePartner.Name))
            {
                // nothing
                SendReqCounterOffer(new ObjectID[0]);

                // tell admin
                SendSayGroupMessage(
                    Data.Trade.TradePartner.ID,
                    "I will take that, master " + Data.Trade.TradePartner.Name);
            }
        }

        /// <summary>
        /// Handles someone offers you first
        /// </summary>
        /// <param name="Message"></param>
        protected override void HandleBuyList(BuyListMessage Message)
        {
            base.HandleBuyList(Message);

            //Check if we currently want to buy something
            if (!buyRequests.Any()) return;

            List<BuyRequest> buyThisList = new List<BuyRequest>();

            //Assemble List of reas we want to buy
            foreach (BuyRequest request in buyRequests)
            {
                //Must be from our requested NPC
                if (request.TargetId == Message.TradePartner.ID)
                {
                    //Look for the Item we want
                    foreach (TradeOfferObject item in Message.OfferedItems)
                    {
                        if (item.Name.ToLowerInvariant().Equals(request.Reagent))
                        {
                            // Check the inventory for the reagent
                            InventoryObject inventoryObject =
                                    Data.InventoryObjects.GetItemByName(request.Reagent, false);

                            // Reas in inventory found, calculate how much to get to reach max amount
                            if (inventoryObject != null)
                            {
                                var currentInventoryAmount = inventoryObject.Count;
                                // More or equal reas in inventory then max
                                if (request.Max <= currentInventoryAmount)
                                {
                                    Log("WARN", "You have the max amount of " + currentInventoryAmount + " " + request.Reagent + " in your inventory!");
                                    break;
                                }
                                //Calculate new wanted amount to get
                                request.RealGetAmount = request.Max - currentInventoryAmount;
                            }
                            // No reas in inventory, desired amount is max amount
                            else
                            {
                                request.RealGetAmount = request.Max;
                            }
                            
                            // The item Id with that needs to be requested from the NPC
                            request.ItemId = item.ID;
                            // Add to list of items to buy
                            buyThisList.Add(request);
                        }
                    }
                }
            }

            //This is what we going to buy
            if (buyThisList.Any())
            {
                var itemsToBuy = buyThisList.Select(x => new ObjectID(x.ItemId, x.RealGetAmount)).ToArray();
                SendReqBuyItemsMessage(Message.TradePartner.ID, itemsToBuy);
            }
        }

        /// <summary>
        /// Handles a counteroffer (you offered first)
        /// </summary>
        /// <param name="Message"></param>
        protected override void HandleCounterOfferMessage(CounterOfferMessage Message)
        {
            base.HandleCounterOfferMessage(Message);

            if (Data.Trade.TradePartner == null)
                return;

            // accept anything from configured admins in config
            if (Config.IsAdmin(Data.Trade.TradePartner.Name))
            {
                // tell admin
                SendSayGroupMessage(
                    Data.Trade.TradePartner.ID,
                    "Thank you, master " + Data.Trade.TradePartner.Name);

                // accept
                SendAcceptOffer();
            }
        }

        /// <summary>
        /// Handles a new player enters room
        /// </summary>
        /// <param name="Message"></param>
        protected override void HandleCreateMessage(CreateMessage Message)
        {
            base.HandleCreateMessage(Message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Message"></param>
        protected override void HandleMessageMessage(MessageMessage Message)
        {
            base.HandleMessageMessage(Message);

            if (Message.Message.FullString.Contains(ChatSubStrings.IMPROVED))
                imps++;

            Log("CHAT", Message.Message.FullString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="PartnerID"></param>
        /// <param name="Words"></param>
        protected override void ProcessCommand(uint PartnerID, string[] Words)
        {
            
        }

        /// <summary>
        /// Overrides Update from BaseClient to tasks in intervals.
        /// </summary>
        public override void Update()
        {
            base.Update();
           
            // ...
            if (!Data.IsWaiting &&
                ObjectID.IsValid(Data.AvatarID) &&
                Data.SpellObjects.Count > 0 &&
                Data.AvatarSpells.Count > 0 &&
                GameTick.Current > tickSleepUntil)
            {
                // get next task
                currentTask = Config.GetNextTask();

                // no task?
                if (currentTask == null)
                {
                    // log
                    Log("ERROR", "No task found.");

                    return;
                }

                // rest
                if (currentTask is BotTaskRest)                
                    DoRest((BotTaskRest)currentTask);
                
                // stand
                else if (currentTask is BotTaskStand)                
                    DoStand((BotTaskStand)currentTask);
                
                // sleep: blocks executions until sleep is over
                else if (currentTask is BotTaskSleep)               
                    DoSleep((BotTaskSleep)currentTask);
                
                // cast
                else if (currentTask is BotTaskCast)               
                    DoCast((BotTaskCast)currentTask);                 
                
                // use
                else if (currentTask is BotTaskUse)                
                    DoUse((BotTaskUse)currentTask);
                
                // say
                else if (currentTask is BotTaskSay)               
                    DoSay((BotTaskSay)currentTask);
                
                // get reas from storage
                else if(currentTask is BotTaskStorageGet)
                    DoGetReasStorage((BotTaskStorageGet)currentTask);
            }

            double slp = (tickSleepUntil - GameTick.Current) / (double)Common.GameTick.MSINSECOND;
            
            // draw sleep & imps
            DrawDynamic("SLP: " + string.Format("{0:N0}", slp).PadRight(3) + " IMPS: " + imps.ToString().PadRight(3));
        }

        protected void DoGetReasStorage(BotTaskStorageGet Task)
        {
            string npcName = Task.Target;
            string rea = Task.Reagent;
            uint maxAmount = Task.Max;
            uint minAmount = Task.Min;
            // try to get the target
            RoomObject roomObject =
                    Data.RoomObjects.GetItemByName(npcName, false);

            // target npc not found
            if (roomObject == null)
            {
                // log
                Log("WARN", "Can't execute task 'storageget'. RoomObject " + npcName + " not found.");

                return;
            }
            // check if minimum amount is reached
            InventoryObject inventoryObject =
                    Data.InventoryObjects.GetItemByName(rea, false);

            // Have no more or too little
            if(inventoryObject == null || inventoryObject.Count <= minAmount)
            {
                // add pending buy request
                buyRequests.Add(new BuyRequest(rea, roomObject.ID, maxAmount));

                // select the NPC as Target
                Data.TargetID = roomObject.ID;

                // send buy request to NPC
                SendReqBuyMessage();

            }
            // Have still enough
            else
            {
                Log("BOT", "Have still "+ inventoryObject.Count + " " + inventoryObject.Name + ". Minimum of "+ Task.Min + " not reached!" );
            }
        }

        /// <summary>
        /// Executes a Task 'rest'
        /// </summary>
        protected void DoRest(BotTaskRest Task)
        {
            // request to rest
            SendUserCommandRest();

            // log
            Log("BOT", "Executed task 'rest'.");
        }

        /// <summary>
        /// Executes a Task 'stand'
        /// </summary>
        /// <param name="Task"></param>
        protected void DoStand(BotTaskStand Task)
        {
            SendUserCommandStand();

            // log
            Log("BOT", "Executed task 'stand'.");
        }

        /// <summary>
        /// Executes a Task 'sleep'
        /// </summary>
        /// <param name="Task"></param>
        protected void DoSleep(BotTaskSleep Task)
        {
            // set sleep tick
            tickSleepUntil = GameTick.Current + Task.Duration;

            // log
            Log("BOT", "Executed task 'sleep' " + (Task.Duration / 1000).ToString() + ".");
        }

        /// <summary>
        /// Executes a Task 'cast'
        /// </summary>
        /// <param name="Task"></param>
        protected void DoCast(BotTaskCast Task)
        {
            SpellObject spellObject = null;
            StatList spellStat = null;
            string sureTarget = Task.Target;
            string sureWhere = Task.Where;

            // handle selftargeting
            if (sureTarget.ToLower().Equals(SpellBotConfig.XMLVALUE_SELF))
            {
                if(Data.AvatarObject == null)
                {
                    Log("WARN", "Cant execute task 'cast' " + Task.Name + ". Technical interruption changed the target.");
                    return;
                }

                sureTarget = Data.AvatarObject.Name.ToLower();
                sureWhere = SpellBotConfig.XMLVALUE_ROOM;
            }

            // try to get the spell from the spells
            spellObject = Data.SpellObjects.GetItemByName(Task.Name, false);

            // try to get stat for % value
            if (spellObject != null)
                spellStat = Data.AvatarSpells.GetItemByID(spellObject.ID);

            // one not found
            if (spellObject == null || spellStat == null)
            {
                // log
                Log("WARN", "Cant execute task 'cast'. Spell " + Task.Name + " found.");

                return;
            }
           
            // handle spells above cap
            if (spellStat.SkillPoints >= Task.Cap)
            {
                if (Task.OnMax.ToLower() == SpellBotConfig.XMLVALUE_QUIT)
                {
                    // log
                    Log("BOT", "Quitting.. spell " + spellObject.Name + " reached 99%.");

                    // prepare quit
                    IsRunning = false;
                    return;
                }
                else if (Task.OnMax.ToLower() == SpellBotConfig.XMLVALUE_SKIP)
                {
                    // log
                    Log("BOT", "Skipped task 'cast' " + spellObject.Name + " (99%)");

                    return;
                }
            }

            // spell doesn't need a target
            if (spellObject.TargetsCount == 0)
            {
                // send castreq
                SendReqCastMessage(spellObject);

                // log
                Log("BOT", "Executed task 'cast' " + spellObject.Name);
            }

            // speed needs a target
            else if (spellObject.TargetsCount > 0)
            {
                // marked to cast on roomobject
                if (sureWhere == SpellBotConfig.XMLVALUE_ROOM)
                {
                    // try to get the target
                    RoomObject roomObject =
                        Data.RoomObjects.GetItemByName(sureTarget, false);

                    // target not found
                    if (roomObject == null)
                    {
                        // log
                        Log("WARN", "Can't execute task 'cast'. RoomObject " + sureTarget + " not found.");

                        return;
                    }
                 
                    // send castreq
                    ReqCastMessage reqCastMsg = new ReqCastMessage(
                        spellObject.ID, new ObjectID[] { new ObjectID(roomObject.ID) });

                    SendGameMessage(reqCastMsg);

                    // log
                    Log("BOT", "Executed task 'cast' " + spellObject.Name);
                }

                // cast on inventory item
                else if (sureWhere == SpellBotConfig.XMLVALUE_INVENTORY)
                {
                    // try to get the target
                    InventoryObject inventoryObject =
                        Data.InventoryObjects.GetItemByName(sureTarget, false);

                    // target not found
                    if (inventoryObject == null)
                    {
                        // log
                        Log("WARN", "Can't execute task 'cast'. Item " + sureTarget + " not found.");

                        return;
                    }

                    // send castreq
                    ReqCastMessage reqCastMsg = new ReqCastMessage(
                        spellObject.ID, new ObjectID[] { new ObjectID(inventoryObject.ID) });

                    SendGameMessage(reqCastMsg);

                    // log
                    Log("BOT", "Executed task 'cast' " + spellObject.Name);
                }
                else
                {
                    // log
                    Log("WARN", "Can't execute task 'cast'. " + sureWhere + " is unknown 'where'.");
                }
            }
        }

        /// <summary>
        /// Executes a Task 'use'
        /// </summary>
        /// <param name="Task"></param>
        protected void DoUse(BotTaskUse Task)
        {          
            // try to get the item from the inventory
            InventoryObject inventoryObject =
                Data.InventoryObjects.GetItemByName(Task.Name, false);

            // item not found
            if (inventoryObject == null)
            {
                // log
                Log("WARN", "Cant execute task 'use'. Item " + Task.Name + " not found.");

                return;
            }

            // send requse
            if (!inventoryObject.IsInUse)
                SendReqUseMessage(inventoryObject.ID);
            
            // send requnuse
            else
                SendReqUnuseMessage(inventoryObject.ID);

            // log
            Log("BOT", "Executed task 'use' " + inventoryObject.Name);
        }

        /// <summary>
        /// Executes a Task 'say'
        /// </summary>
        /// <param name="Task"></param>
        protected void DoSay(BotTaskSay Task)
        {
            // send say
            SendSayToMessage(ChatTransmissionType.Normal, Task.Text);

            // log
            Log("BOT", "Executed task 'say': " + Task.Text);
        }
    }

    public class BuyRequest
    {
        //Name of reagent to get
        public string Reagent { get; }
        /// <summary>
        /// The Name of the NPC to get from
        /// </summary>
        public uint TargetId { get; }

        /// <summary>
        /// The maximum amount in inventory
        /// </summary>
        public uint Max { get; }

        /// <summary>
        /// Gets or sets the item identifier to request from the NPC.
        /// </summary>
        /// <value>
        /// The item identifier.
        /// </value>
        public uint ItemId { get; set; }

        /// <summary>
        /// Gets or sets the Amount to request from the NPC.
        /// </summary>
        /// <value>
        /// The real amount to get from the NPC.
        /// </value>
        public uint RealGetAmount { get; set; } = 0;
        public BuyRequest(string reagent, uint targetId, uint max)
        {
            Reagent = reagent.ToLowerInvariant();
            TargetId = targetId;
            Max = max;
        }
    }
}
