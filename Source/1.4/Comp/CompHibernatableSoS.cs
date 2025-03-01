﻿using RimWorld.Planet;
using SaveOurShip2;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimWorld
{
    public class CompHibernatableSoS : ThingComp
    {
        private HibernatableStateDef state = HibernatableStateDefOf.Hibernating;

        private int endStartupTick;

        private IntVec3 explosionPos;

        public CompProperties_HibernatableSoS Props
        {
            get
            {
                return (CompProperties_HibernatableSoS)this.props;
            }
        }

        public HibernatableStateDef State
        {
            get
            {
                return this.state;
            }
            set
            {
                if (this.state == value)
                {
                    return;
                }
                this.state = value;
                this.parent.Map.info.parent.Notify_HibernatableChanged();
            }
        }

        public bool Running
        {
            get
            {
                return this.State == HibernatableStateDefOf.Running;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                this.parent.Map.info.parent.Notify_HibernatableChanged();
            }
            explosionPos = this.parent.Position;
        }

        public override void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
            map.info.parent.Notify_HibernatableChanged();
        }

        public void Startup()
        {
            if (this.State != HibernatableStateDefOf.Hibernating)
            {
                Log.ErrorOnce("Attempted to start a non-hibernating object", 34361223);
                return;
            }
            this.State = HibernatableStateDefOf.Starting;
            this.endStartupTick = Mathf.RoundToInt((float)Find.TickManager.TicksGame + this.Props.startupDays * 60000f);
        }

        public override string CompInspectStringExtra()
        {
            if (this.State == HibernatableStateDefOf.Hibernating)
            {
                return TranslatorFormattedStringExtensions.Translate("SoSHibernatableHibernating");
            }
            if (this.State == HibernatableStateDefOf.Starting)
            {
                return string.Format("{0}: {1}", TranslatorFormattedStringExtensions.Translate("SoSHibernatableStartingUp"), (this.endStartupTick - Find.TickManager.TicksGame).ToStringTicksToPeriod());
            }
            return null;
        }

        public override void CompTick()
        {
            if (this.State == HibernatableStateDefOf.Starting && Find.TickManager.TicksGame > this.endStartupTick)
            {
                this.State = HibernatableStateDefOf.Running;
                this.endStartupTick = 0;
                string text = TranslatorFormattedStringExtensions.Translate("LetterSoSHibernateCompleteStandalone");
                Find.LetterStack.ReceiveLetter(TranslatorFormattedStringExtensions.Translate("LetterLabelSoSHibernateComplete"), text, LetterDefOf.PositiveEvent, new GlobalTargetInfo(this.parent), null, null);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Defs.Look<HibernatableStateDef>(ref this.state, "hibernateState");
            Scribe_Values.Look<int>(ref this.endStartupTick, "hibernateendStartupTick", 0, false);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            List<Gizmo> gizmos = new List<Gizmo>();
            foreach (Gizmo giz in base.CompGetGizmosExtra())
                gizmos.Add(giz);
            if (this.state == HibernatableStateDefOf.Hibernating)
            {
                Command_Action discharge = new Command_Action();
                discharge.action = delegate
                    {
                        string text = "SoSHibernateWarningStandalone";
                        DiaNode diaNode = new DiaNode(text.Translate());
                        DiaOption diaOption = new DiaOption(TranslatorFormattedStringExtensions.Translate("Confirm"));
                        diaOption.action = delegate
                        {
                            this.Startup();
                        };
                        diaOption.resolveTree = true;
                        diaNode.options.Add(diaOption);
                        DiaOption diaOption2 = new DiaOption(TranslatorFormattedStringExtensions.Translate("GoBack"));
                        diaOption2.resolveTree = true;
                        diaNode.options.Add(diaOption2);
                        Find.WindowStack.Add(new Dialog_NodeTree(diaNode, true, false, null));
                    };
                discharge.defaultLabel = TranslatorFormattedStringExtensions.Translate("SoSCommandShipStartup");
                discharge.defaultDesc = TranslatorFormattedStringExtensions.Translate("SoSCommandShipStartupDesc");
                discharge.hotKey = KeyBindingDefOf.Misc1;
                discharge.icon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower", true);
                gizmos.Add(discharge);
            }
            return gizmos;
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            bool doneSafely = false;
            if(mode==DestroyMode.Deconstruct)
            {
                if (state == HibernatableStateDefOf.Running)
                {
                    doneSafely = true;
                    //Find.LetterStack.ReceiveLetter("Feature incomplete", "In future versions of Save Our Ship, this drive will drop salvage to be reverse-engineered using a salvage bay. For now, you just get the tech for free. Hooray!", LetterDefOf.PositiveEvent);
                    //WorldSwitchUtility.PastWorldTracker.Unlocks.Add("JTDrive");
                    Thing notTechprint = ThingMaker.MakeThing(ThingDef.Named("SoSEntanglementManifold"));
                    WorldSwitchUtility.PastWorldTracker.Unlocks.Add("JTDriveToo");
                    GenPlace.TryPlaceThing(notTechprint, this.parent.Position, previousMap, ThingPlaceMode.Near);
                }
            }
            if(!doneSafely)
            {
                GenExplosion.DoExplosion(this.explosionPos, previousMap, 15, DamageDefOf.Bomb, null);
                Find.LetterStack.ReceiveLetter(TranslatorFormattedStringExtensions.Translate("ImpactSiteLostLabel"), TranslatorFormattedStringExtensions.Translate("ImpactSiteLost"), LetterDefOf.NegativeEvent);
                ShipInteriorMod2.GenerateImpactSite();
            }
        }
    }
}
