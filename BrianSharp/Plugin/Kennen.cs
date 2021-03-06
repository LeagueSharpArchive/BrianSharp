﻿using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = BrianSharp.Common.Orbwalker;

namespace BrianSharp.Plugin
{
    class Kennen : Common.Helper
    {
        public Kennen()
        {
            Q = new Spell(SpellSlot.Q, 1050);
            W = new Spell(SpellSlot.W, 910);
            E = new Spell(SpellSlot.E);
            R = new Spell(SpellSlot.R, 560);
            Q.SetSkillshot(0.125f, 50, 1700, true, SkillshotType.SkillshotLine);
            W.SetSkillshot(0.5f, 910, float.MaxValue, false, SkillshotType.SkillshotCircle);
            R.SetSkillshot(0.5f, 560, 779.9f, false, SkillshotType.SkillshotCircle);

            var ChampMenu = new Menu("Plugin", PlayerName + "_Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    AddItem(ComboMenu, "Q", "Use Q");
                    AddItem(ComboMenu, "W", "Use W");
                    AddItem(ComboMenu, "R", "Use R");
                    AddItem(ComboMenu, "RHpU", "-> If Enemy Hp Under", 60);
                    AddItem(ComboMenu, "RCountA", "-> If Enemy Above", 2, 1, 5);
                    AddItem(ComboMenu, "RItem", "-> Use Zhonya When R Active");
                    AddItem(ComboMenu, "RItemHpU", "--> If Hp Under", 60);
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    AddItem(HarassMenu, "AutoQ", "Use Q", "H", KeyBindType.Toggle);
                    AddItem(HarassMenu, "AutoQMpA", "-> If Mp Above", 50);
                    AddItem(HarassMenu, "Q", "Use Q");
                    AddItem(HarassMenu, "W", "Use W");
                    AddItem(HarassMenu, "WMpA", "-> If Mp Above", 50);
                    ChampMenu.AddSubMenu(HarassMenu);
                }
                var ClearMenu = new Menu("Lane/Jungle Clear", "Clear");
                {
                    AddItem(ClearMenu, "Q", "Use Q");
                    AddItem(ClearMenu, "W", "Use W");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var LastHitMenu = new Menu("Last Hit", "LastHit");
                {
                    AddItem(LastHitMenu, "Q", "Use Q");
                    ChampMenu.AddSubMenu(LastHitMenu);
                }
                var FleeMenu = new Menu("Flee", "Flee");
                {
                    AddItem(FleeMenu, "E", "Use E");
                    AddItem(FleeMenu, "W", "Use W To Stun Enemy");
                    ChampMenu.AddSubMenu(FleeMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    var KillStealMenu = new Menu("Kill Steal", "KillSteal");
                    {
                        AddItem(KillStealMenu, "Q", "Use Q");
                        AddItem(KillStealMenu, "W", "Use W");
                        AddItem(KillStealMenu, "R", "Use R");
                        AddItem(KillStealMenu, "Ignite", "Use Ignite");
                        MiscMenu.AddSubMenu(KillStealMenu);
                    }
                    var InterruptMenu = new Menu("Interrupt", "Interrupt");
                    {
                        AddItem(InterruptMenu, "W", "Use W");
                        foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsEnemy))
                        {
                            foreach (var Spell in Interrupter.Spells.Where(i => i.ChampionName == Obj.ChampionName)) AddItem(InterruptMenu, Obj.ChampionName + "_" + Spell.Slot.ToString(), "-> Skill " + Spell.Slot.ToString() + " Of " + Obj.ChampionName);
                        }
                        MiscMenu.AddSubMenu(InterruptMenu);
                    }
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    AddItem(DrawMenu, "Q", "Q Range", false);
                    AddItem(DrawMenu, "W", "W Range", false);
                    AddItem(DrawMenu, "R", "R Range", false);
                    ChampMenu.AddSubMenu(DrawMenu);
                }
                MainMenu.AddSubMenu(ChampMenu);
            }
            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsRecalling()) return;
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo(Orbwalk.CurrentMode.ToString());
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear)
            {
                LaneJungClear();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LastHit)
            {
                LastHit();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.Flee) Flee();
            if (GetValue<KeyBind>("Harass", "AutoQ").Active) AutoQ();
            KillSteal();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (GetValue<bool>("Draw", "Q") && Q.Level > 0) Render.Circle.DrawCircle(Player.Position, Q.Range, Q.IsReady() ? Color.Green : Color.Red);
            if (GetValue<bool>("Draw", "W") && W.Level > 0) Render.Circle.DrawCircle(Player.Position, W.Range, W.IsReady() ? Color.Green : Color.Red);
            if (GetValue<bool>("Draw", "R") && R.Level > 0) Render.Circle.DrawCircle(Player.Position, R.Range, R.IsReady() ? Color.Green : Color.Red);
        }

        private void OnPossibleToInterrupt(Obj_AI_Hero unit, InterruptableSpell spell)
        {
            if (Player.IsDead || !GetValue<bool>("Interrupt", "W") || !GetValue<bool>("Interrupt", unit.ChampionName + "_" + spell.Slot.ToString()) || !W.CanCast(unit) || !unit.HasBuff("KennenMarkOfStorm")) return;
            if (HaveWStun(unit) && W.Cast(PacketCast))
            {
                return;
            }
            else if (!HaveWStun(unit) && Q.CastIfHitchanceEquals(unit, HitChance.High, PacketCast)) return;
        }

        private void NormalCombo(string Mode)
        {
            if (GetValue<bool>(Mode, "Q") && Q.CastOnBestTarget(0, PacketCast).IsCasted()) return;
            if (GetValue<bool>(Mode, "W") && W.IsReady() && ObjectManager.Get<Obj_AI_Hero>().Any(i => i.IsValidTarget(W.Range) && i.HasBuff("KennenMarkOfStorm")) && (Mode == "Combo" || (Mode == "Harass" && Player.ManaPercentage() >= GetValue<Slider>(Mode, "WMpA").Value)))
            {
                if (Player.HasBuff("KennenShurikenStorm"))
                {
                    var Target = ObjectManager.Get<Obj_AI_Hero>().FindAll(i => i.IsValidTarget(W.Range) && i.HasBuff("KennenMarkOfStorm"));
                    if ((Target.Count(i => CanKill(i, W, 1)) > 0 || Target.Count(i => HaveWStun(i)) > 1 || Target.Count > 2 || (Target.Count(i => HaveWStun(i)) == 1 && Target.Count(i => !HaveWStun(i)) > 0)) && W.Cast(PacketCast)) return;
                }
                else if (W.Cast(PacketCast)) return;
            }
            if (Mode == "Combo" && GetValue<bool>(Mode, "R"))
            {
                if (R.IsReady())
                {
                    var Target = ObjectManager.Get<Obj_AI_Hero>().FindAll(i => i.IsValidTarget(R.Range));
                    if (((Target.Count > 1 && Target.Count(i => CanKill(i, R, GetRDmg(i))) > 0) || Target.Count >= GetValue<Slider>(Mode, "RCountA").Value || (Target.Count > 1 && Target.Count(i => i.HealthPercentage() < GetValue<Slider>(Mode, "RHpU").Value) > 0)) && R.Cast(PacketCast)) return;
                }
                else if (Player.HasBuff("KennenShurikenStorm") && GetValue<bool>(Mode, "RItem") && Player.HealthPercentage() < GetValue<Slider>(Mode, "RItemHpU").Value && R.GetTarget() != null && Zhonya.IsReady() && Zhonya.Cast()) return;
            }
        }

        private void LaneJungClear()
        {
            var minionObj = MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth);
            if (minionObj.Count == 0) return;
            if (GetValue<bool>("Clear", "Q") && Q.IsReady())
            {
                var Obj = minionObj.Find(i => CanKill(i, Q));
                if (Obj == null) Obj = minionObj.Find(i => i.MaxHealth >= 1200);
                if (Obj != null && Q.CastIfHitchanceEquals(Obj, HitChance.Medium, PacketCast)) return;
            }
            if (GetValue<bool>("Clear", "W") && W.IsReady() && minionObj.Count(i => W.IsInRange(i) && i.HasBuff("KennenMarkOfStorm")) > 1 && W.Cast(PacketCast)) return;
        }

        private void LastHit()
        {
            if (!GetValue<bool>("LastHit", "Q") || !Q.IsReady()) return;
            var minionObj = MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth).FindAll(i => CanKill(i, Q));
            if (minionObj.Count == 0 || Q.CastIfHitchanceEquals(minionObj.First(), HitChance.High, PacketCast)) return;
        }

        private void Flee()
        {
            if (GetValue<bool>("Flee", "E") && E.IsReady() && E.Instance.Name == "KennenLightningRush" && E.Cast(PacketCast)) return;
            if (GetValue<bool>("Flee", "W") && W.IsReady() && ObjectManager.Get<Obj_AI_Hero>().Any(i => i.IsValidTarget(W.Range) && i.HasBuff("KennenMarkOfStorm") && HaveWStun(i)) && W.Cast(PacketCast)) return;
        }

        private void AutoQ()
        {
            if (Player.ManaPercentage() < GetValue<Slider>("Harass", "AutoQMpA").Value || Q.CastOnBestTarget(0, PacketCast).IsCasted()) return;
        }

        private void KillSteal()
        {
            if (GetValue<bool>("KillSteal", "Ignite") && Ignite.IsReady())
            {
                var Target = TargetSelector.GetTarget(600, TargetSelector.DamageType.True);
                if (Target != null && CastIgnite(Target)) return;
            }
            if (GetValue<bool>("KillSteal", "Q") && Q.IsReady())
            {
                var Target = Q.GetTarget();
                if (Target != null && CanKill(Target, Q) && Q.CastIfHitchanceEquals(Target, HitChance.High, PacketCast)) return;
            }
            if (GetValue<bool>("KillSteal", "W") && W.IsReady())
            {
                var Target = W.GetTarget();
                if (Target != null && Target.HasBuff("KennenMarkOfStorm") && CanKill(Target, W, 1) && W.Cast(PacketCast)) return;
            }
            if (GetValue<bool>("KillSteal", "R") && R.IsReady())
            {
                var Target = R.GetTarget();
                if (Target != null && CanKill(Target, R, GetRDmg(Target)) && R.Cast(PacketCast)) return;
            }
        }

        private double GetRDmg(Obj_AI_Hero Target)
        {
            return Player.CalcDamage(Target, Damage.DamageType.Magical, (new double[] { 80, 145, 210 }[R.Level - 1] + 0.4 * Player.FlatMagicDamageMod) * 3);
        }

        private bool HaveWStun(Obj_AI_Base Target)
        {
            return Target.Buffs.First(a => a.DisplayName == "KennenMarkOfStorm").Count == 2;
        }
    }
}