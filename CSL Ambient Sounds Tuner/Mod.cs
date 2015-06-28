﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AmbientSoundsTuner.Compatibility;
using AmbientSoundsTuner.UI;
using AmbientSoundsTuner.Utils;
using ColossalFramework.Plugins;
using CommonShared;
using CommonShared.Utils;
using ICities;
using UnityEngine;

namespace AmbientSoundsTuner
{
    public class Mod : LoadingExtensionBase, IUserMod
    {
        internal static Configuration Settings { get; private set; }
        internal static string SettingsFilename { get; private set; }
        internal static Logger Log { get; private set; }
        internal static Mod Instance { get; private set; }

        internal SoundsInstanceAmbientsPatcher AmbientsPatcher { get; private set; }
        internal SoundsInstanceEffectsPatcher EffectsPatcher { get; private set; }
        internal SoundsInstanceMiscellaneousPatcher MiscellaneousPatcher { get; private set; }

        internal static HashSet<ulong> IncompatibleMods = new HashSet<ulong>()
        {
            421527612, // SilenceObnoxiousSirens
        };

        private bool isLoaded = false;
        private bool isActivated = false;

        public string Name
        {
            get
            {
                // Here we load our stuff, hacky, but oh well...
                this.Init();

                if (!this.isLoaded)
                {
                    PluginUtils.SubscribePluginStateChange(this, isEnabled =>
                    {
                        if (isEnabled) this.Load();
                        else this.Unload();
                    });
                }

                if (!this.isActivated)
                {
                    var pluginInfo = PluginUtils.GetPluginInfo(this);
                    if (pluginInfo.isEnabled)
                    {
                        this.isActivated = true;
                        this.Load();
                    }
                }

                return "Ambient Sounds Tuner";
            }
        }

        public string Description
        {
            get { return "Tune your ambient sounds volumes individually"; }
        }


        private void Init()
        {
            SettingsFilename = Path.Combine(FileUtils.GetStorageFolder(this), "AmbientSoundsTuner.xml");
            Log = new Logger(this);
            Instance = this;

            this.AmbientsPatcher = new SoundsInstanceAmbientsPatcher();
            this.EffectsPatcher = new SoundsInstanceEffectsPatcher();
            this.MiscellaneousPatcher = new SoundsInstanceMiscellaneousPatcher();
        }

        private void Load()
        {
            this.CheckIncompatibility();

            Mod.Settings = Config.LoadConfig<Configuration>(Mod.SettingsFilename);
            Mod.Log.EnableDebugLogging = Mod.Settings.ExtraDebugLogging;

            if (Mod.Settings.ExtraDebugLogging)
            {
                Mod.Log.Warning("Extra debug logging is enabled, please use this only to get more information while hunting for bugs; don't use this when playing normally!");
            }

            AdvancedOptions.CreateAdvancedOptions();
        }

        private void Unload()
        {
            AdvancedOptions.DestroyAdvancedOptions();
        }


        private void CheckIncompatibility()
        {
            var list = PluginUtils.GetPluginInfosOf(IncompatibleMods);
            if (list.Count > 0)
            {
                string text = string.Join(", ",
                    list.Where(kvp => kvp.Value.isEnabled)
                        .Select(kvp => string.Format("{0} ({1})", kvp.Value.GetInstances<IUserMod>()[0].Name, kvp.Value.publishedFileID.AsUInt64.ToString()))
                        .OrderBy(s => s)
                        .ToArray());

                if (text != "")
                {
                    Mod.Log.Warning("You've got some known incompatible mods enabled! It's possible that this mod doesn't work as expected.\nThe following incompatible mods are enabled: {0}.", text);
                }
            }
        }


        /// <summary>
        /// Our entry point. Here we load the mod.
        /// </summary>
        /// <param name="mode">The game mode.</param>
        public override void OnLevelLoaded(LoadMode mode)
        {
            base.OnLevelLoaded(mode);
            this.Load();
            PatchAmbientSounds();
            PatchEffectSounds();
            PatchMiscellaneousSounds();
        }

        /// <summary>
        /// Our exit point. Here we unload everything we have loaded.
        /// </summary>
        public override void OnLevelUnloading()
        {
            base.OnLevelUnloading();
            Settings.SaveConfig(Mod.SettingsFilename);

            // Set isLoaded to false again so the mod will load again at the main menu
            this.isLoaded = false;
        }


        private void PatchSounds<T>(SoundsInstancePatcher<T> patcher, IDictionary<T, float> newVolumes)
        {
            int soundsCount = patcher.DefaultVolumes.Count;
            int backedUpCount = patcher.BackupVolumes();
            if (backedUpCount < soundsCount)
            {
                Mod.Log.Warning("{0}/{1} sound volumes have been backed up", backedUpCount, soundsCount);
            }

            int patchedCount = patcher.PatchVolumes(newVolumes);
            if (patchedCount < soundsCount)
            {
                Mod.Log.Warning("{0}/{1} sound volumes have been patched", patchedCount, soundsCount);
            }
        }

        internal void PatchAmbientSounds()
        {
            this.PatchSounds(this.AmbientsPatcher, Settings.State.AmbientVolumes);
            Mod.Log.Info("Ambient sound volumes have been patched");
        }

        internal void PatchEffectSounds()
        {
            // Patch the sirens for compatibility first!
            var patchResult = SirensPatcher.PatchPoliceSiren();
            switch (patchResult)
            {
                case SirensPatcher.PatchResult.Success:
                    Mod.Log.Info("Police sirens have been patched");
                    break;
                case SirensPatcher.PatchResult.AlreadyPatched:
                    Mod.Log.Info("Police sirens have been patched already");
                    break;
                case SirensPatcher.PatchResult.NotFound:
                    Mod.Log.Warning("Could not patch the police sirens to be different from the ambulance sirens");
                    break;
            }

            this.PatchSounds(this.EffectsPatcher, Settings.State.EffectVolumes);
            Mod.Log.Info("Effect sound volumes have been patched");
        }

        internal void PatchMiscellaneousSounds()
        {
            this.PatchSounds(this.MiscellaneousPatcher, Settings.State.MiscellaneousVolumes);
            Mod.Log.Info("Miscellaneous sound volumes have been patched");
        }
    }
}
