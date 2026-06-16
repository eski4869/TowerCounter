using System;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using EntityComponent;
using JumpKing;
using JumpKing.API;
using JumpKing.GameManager;
using JumpKing.MiscSystems.LocationText;
using JumpKing.Mods;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT.Actions;
using JumpKing.Player;
using JumpKing.Util;
using JumpKing.Util.Tags;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace TowerCounter
{
    [JumpKingMod("eski4869.TowerCounter")]
    public static class ModEntry
    {
        private const string SettingsFileName = "eski4869.TowerCounter.Settings.xml";

        private static TowerCounterBehaviour _registeredBehaviour;
        private static string _assemblyPath;
        private static string _settingsPath;
        private static bool _settingsDirty;
        private static bool _processExitRegistered;

        public static Preferences Preferences { get; private set; }

        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            EnsurePreferencesLoaded();
        }

        [OnLevelStart]
        public static void OnLevelStart()
        {
            TowerCounterDisplay.Enabled = Preferences.IsEnabled;
            new TowerCounterDisplay();
            TowerCounterBehaviour.EnsureCreated();

            if (!Preferences.IsEnabled)
            {
                UnregisterTowerBehaviour();
                return;
            }

            RegisterTowerBehaviour();
        }

        private static void RegisterTowerBehaviour()
        {
            PlayerEntity player = EntityManager.instance.Find<PlayerEntity>();

            if (player == null)
            {
                return;
            }

            TowerCounterBehaviour existingBehaviour = player.GetComponent<TowerCounterBehaviour>();

            if (existingBehaviour != null)
            {
                _registeredBehaviour = existingBehaviour;
                return;
            }

            _registeredBehaviour = TowerCounterBehaviour.Instance;
            player.AddComponents(new Component[] { _registeredBehaviour });
        }

        private static void UnregisterTowerBehaviour()
        {
            _registeredBehaviour = null;
        }

        [OnLevelEnd]
        public static void OnLevelEnd()
        {
            SaveSettingsIfDirty();
        }

        [OnLevelUnload]
        public static void OnLevelUnload()
        {
            SaveSettingsIfDirty();
        }

        [PauseMenuItemSetting]
        [MainMenuItemSetting]
        public static DisplayCounterToggle DisplayCounterMenu(object factory, GuiFormat format)
        {
            return new DisplayCounterToggle();
        }

        public static bool IsDisplayEnabled()
        {
            EnsurePreferencesLoaded();
            return Preferences.IsEnabled;
        }

        public static void SetDisplayEnabled(bool isEnabled)
        {
            EnsurePreferencesLoaded();

            if (Preferences.IsEnabled == isEnabled)
            {
                return;
            }

            Preferences.IsEnabled = isEnabled;
            TowerCounterDisplay.Enabled = isEnabled;
            _settingsDirty = true;

            if (isEnabled)
            {
                TowerCounterBehaviour.ReloadState();
                RegisterTowerBehaviour();
            }
            else
            {
                UnregisterTowerBehaviour();
            }
        }

        public static void SetTowerState(bool hasTower, int count, int entranceScreen)
        {
            EnsurePreferencesLoaded();

            if (Preferences.SetTowerState(hasTower, count, entranceScreen))
            {
                _settingsDirty = true;
            }
        }

        private static void EnsurePreferencesLoaded()
        {
            if (Preferences != null)
            {
                RegisterProcessExit();
                return;
            }

            _assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _settingsPath = Path.Combine(_assemblyPath, SettingsFileName);

            try
            {
                if (File.Exists(_settingsPath))
                {
                    var serializer = new XmlSerializer(typeof(Preferences));

                    using (var stream = File.OpenRead(_settingsPath))
                    {
                        Preferences = (Preferences)serializer.Deserialize(stream);
                    }
                }
            }
            catch
            {
            }

            if (Preferences == null)
            {
                Preferences = new Preferences();
            }

            RegisterProcessExit();
        }

        private static void RegisterProcessExit()
        {
            if (_processExitRegistered)
            {
                return;
            }

            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            _processExitRegistered = true;
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            SaveSettingsIfDirty();
        }

        private static void SaveSettingsIfDirty()
        {
            if (!_settingsDirty || Preferences == null)
            {
                return;
            }

            try
            {
                EnsurePreferencesLoaded();

                var serializer = new XmlSerializer(typeof(Preferences));

                using (var stream = File.Create(_settingsPath))
                {
                    serializer.Serialize(stream, Preferences);
                }

                _settingsDirty = false;
            }
            catch
            {
            }
        }
    }

    public class DisplayCounterToggle : ITextToggle
    {
        public DisplayCounterToggle() : base(ModEntry.IsDisplayEnabled())
        {
        }

        protected override string GetName()
        {
            return "Tower Counter";
        }

        protected override void OnToggle()
        {
            ModEntry.SetDisplayEnabled(toggle);
        }
    }

    public class Preferences
    {
        private bool _isEnabled = true;
        private bool _hasTower;
        private int _count;
        private int _entranceScreen = -1;

        public bool IsEnabled
        {
            get
            {
                return _isEnabled;
            }
            set
            {
                if (_isEnabled == value)
                {
                    return;
                }
                _isEnabled = value;
            }
        }

        public bool HasTower
        {
            get
            {
                return _hasTower;
            }
            set
            {
                if (_hasTower == value)
                {
                    return;
                }
                _hasTower = value;
            }
        }

        public int Count
        {
            get
            {
                return _count;
            }
            set
            {
                if (_count == value)
                {
                    return;
                }
                _count = value;
            }
        }

        public int EntranceScreen
        {
            get
            {
                return _entranceScreen;
            }
            set
            {
                if (_entranceScreen == value)
                {
                    return;
                }
                _entranceScreen = value;
            }
        }

        public bool SetTowerState(bool hasTower, int count, int entranceScreen)
        {
            if (_hasTower == hasTower &&
                _count == count &&
                _entranceScreen == entranceScreen)
            {
                return false;
            }

            _hasTower = hasTower;
            _count = count;
            _entranceScreen = entranceScreen;
            return true;
        }
    }

    public class TowerCounterDisplay : Entity, IForeground
    {
        private static readonly FieldInfo TimerDisplayPositionField = typeof(GameLoop).GetField(
            "TIMER_DISPLAY_POSITION",
            BindingFlags.Static | BindingFlags.NonPublic
        );

        public static bool Enabled = true;

        public void ForegroundDraw()
        {
            if (!Enabled)
            {
                return;
            }

            SpriteFont font = GetFont();

            if (font == null)
            {
                return;
            }

            TextHelper.DrawString(
                font,
                "Tower: " + TowerCounterBehaviour.Count,
                GetDrawPosition(),
                Color.Red,
                Vector2.Zero,
                true
            );
        }

        private static SpriteFont GetFont()
        {
            if (Game1.instance == null || Game1.instance.contentManager == null)
            {
                return null;
            }

            if (Game1.instance.contentManager.font.MenuFont != null)
            {
                return Game1.instance.contentManager.font.MenuFont;
            }

            return Game1.instance.contentManager.font.MenuFontSmall;
        }

        private static Vector2 GetDrawPosition()
        {
            try
            {
                if (TimerDisplayPositionField != null)
                {
                    object value = TimerDisplayPositionField.GetValue(null);

                    if (value is Vector2)
                    {
                        return (Vector2)value + new Vector2(0f, 24f);
                    }
                }
            }
            catch
            {
            }

            return new Vector2(12f, 32f);
        }
    }

    public class TowerCounterBehaviour : Component
    {
        private const int MinScreen = 1;
        private const int MaxScreen = 169;

        private static readonly object Sync = new object();
        private static TowerCounterBehaviour _instance;

        public static TowerCounterBehaviour Instance
        {
            get
            {
                EnsureCreated();
                return _instance;
            }
        }

        public static int Count
        {
            get
            {
                lock (Sync)
                {
                    if (_instance == null)
                    {
                        return 0;
                    }

                    return _instance._count;
                }
            }
        }

        public static void ReloadState()
        {
            lock (Sync)
            {
                EnsureCreated();

                if (_instance != null)
                {
                    _instance.LoadState();
                }
            }
        }

        private Location[] _locations = new Location[0];
        private KeyboardState _previousKeyboardState;

        private bool _hasTower;
        private bool _hasLeftTowerArea;
        private string _towerArea = "Unknown";
        private int _entranceScreen = -1;
        private int _count;

        public static void EnsureCreated()
        {
            lock (Sync)
            {
                if (_instance == null)
                {
                    _instance = new TowerCounterBehaviour();
                }
            }
        }

        public TowerCounterBehaviour()
        {
            _instance = this;
            _locations = LoadLocations();
            LoadState();
        }

        protected override void Update(float p_delta)
        {
            if (!TowerCounterDisplay.Enabled)
            {
                return;
            }

            if (_locations == null || _locations.Length == 0)
            {
                _locations = LoadLocations();
            }

            if (_hasTower && _towerArea == "Unknown")
            {
                RestoreTowerAreaFromEntranceScreen();
            }

            KeyboardState keyboardState = Keyboard.GetState();
            int screen = JumpKing.Camera.CurrentScreen + 1;
            string area = GetAreaNameForScreen(screen);
            PlayerEntity player = EntityManager.instance.Find<PlayerEntity>();
            bool isOnGround = player != null && player.m_body.IsOnGround;

            if (WasKeyPressed(keyboardState, Keys.T))
            {
                MarkTower(screen, area);
            }

            if (WasKeyPressed(keyboardState, Keys.OemPlus) ||
                WasKeyPressed(keyboardState, Keys.Add))
            {
                AdjustCount(1);
            }

            if (WasKeyPressed(keyboardState, Keys.OemMinus) ||
                WasKeyPressed(keyboardState, Keys.Subtract))
            {
                AdjustCount(-1);
            }

            UpdateAutoCount(screen, area, isOnGround);
            _previousKeyboardState = keyboardState;
        }

        private bool WasKeyPressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private void MarkTower(int screen, string area)
        {
            if (screen < MinScreen || screen > MaxScreen || area == "Unknown")
            {
                return;
            }

            _hasTower = true;
            _hasLeftTowerArea = false;
            _towerArea = area;
            _entranceScreen = screen;
            _count = 1;
            SaveState();
        }

        private void AdjustCount(int amount)
        {
            _count += amount;

            if (_count < 0)
            {
                _count = 0;
            }

            SaveState();
        }

        private void UpdateAutoCount(int screen, string area, bool isOnGround)
        {
            if (!_hasTower)
            {
                return;
            }

            if (_towerArea == "Unknown")
            {
                return;
            }

            if (area == "Unknown")
            {
                return;
            }

            if (area != _towerArea)
            {
                _hasLeftTowerArea = true;
                return;
            }

            if (_hasLeftTowerArea && screen == _entranceScreen && isOnGround)
            {
                _count++;
                _hasLeftTowerArea = false;
                SaveState();
            }
        }

        private void LoadState()
        {
            ModEntry.IsDisplayEnabled();
            Preferences preferences = ModEntry.Preferences;

            if (preferences == null || !preferences.HasTower)
            {
                return;
            }

            int count = preferences.Count;
            int entranceScreen = preferences.EntranceScreen;

            if (count < 0)
            {
                count = 0;
            }

            _count = count;
            _entranceScreen = entranceScreen;
            _hasTower = entranceScreen >= MinScreen && entranceScreen <= MaxScreen;
            _hasLeftTowerArea = false;
            RestoreTowerAreaFromEntranceScreen();
        }

        private void SaveState()
        {
            ModEntry.SetTowerState(_hasTower, _count, _entranceScreen);
        }

        private void RestoreTowerAreaFromEntranceScreen()
        {
            if (!_hasTower)
            {
                return;
            }

            string area = GetAreaNameForScreen(_entranceScreen);

            if (area != "Unknown")
            {
                _towerArea = area;
            }
        }

        private Location[] LoadLocations()
        {
            try
            {
                Type managerType = typeof(LocationSettings).Assembly.GetType(
                    "JumpKing.MiscSystems.LocationText.LocationTextManager"
                );

                if (managerType == null)
                {
                    return new Location[0];
                }

                object settingsObject = null;

                PropertyInfo settingsProperty = managerType.GetProperty(
                    "SETTINGS",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                );

                if (settingsProperty != null)
                {
                    settingsObject = settingsProperty.GetValue(null, null);
                }

                if (settingsObject == null)
                {
                    FieldInfo settingsField = managerType.GetField(
                        "_settings",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                    );

                    if (settingsField != null)
                    {
                        settingsObject = settingsField.GetValue(null);
                    }
                }

                if (settingsObject is LocationSettings)
                {
                    LocationSettings settings = (LocationSettings)settingsObject;

                    if (settings.locations != null)
                    {
                        return settings.locations;
                    }
                }
            }
            catch
            {
            }

            return new Location[0];
        }

        private string GetAreaNameForScreen(int screen)
        {
            Location location;

            if (TryGetLocationForScreen(screen, out location))
            {
                return FormatAreaName(location.name);
            }

            return "Unknown";
        }

        private bool TryGetLocationForScreen(int screen, out Location matchedLocation)
        {
            matchedLocation = default(Location);

            if (_locations == null || _locations.Length == 0)
            {
                return false;
            }

            bool found = false;
            int bestStart = int.MinValue;

            for (int i = 0; i < _locations.Length; i++)
            {
                Location location = _locations[i];

                if (screen >= location.start && screen <= location.end)
                {
                    if (location.start > bestStart)
                    {
                        matchedLocation = location;
                        bestStart = location.start;
                        found = true;
                    }
                }
            }

            return found;
        }

        private string FormatAreaName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName))
            {
                return "Unknown";
            }

            string name = rawName;

            if (name.StartsWith("LOCATION_"))
            {
                name = name.Substring("LOCATION_".Length);
            }

            name = name.Replace('_', ' ');

            return name.Trim();
        }
    }
}
