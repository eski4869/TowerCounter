using System;
using System.IO;
using System.Reflection;
using System.Text;
using EntityComponent;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.GameManager;
using JumpKing.MiscSystems.LocationText;
using JumpKing.Mods;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT.Actions;
using JumpKing.Player;
using JumpKing.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace TowerCounter
{
    [JumpKingMod("eski4869.TowerCounter")]
    public static class ModEntry
    {
        private static TowerCounterBehaviour _registeredBehaviour;

        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            TowerCounterBehaviour.EnsureCreated();
            TowerCounterDisplay.EnsureAdded();
        }

        [OnLevelStart]
        public static void OnLevelStart()
        {
            TowerCounterBehaviour.EnsureCreated();
            TowerCounterDisplay.EnsureAdded();
            PlayerEntity player = EntityManager.instance.Find<PlayerEntity>();

            if (player == null)
            {
                return;
            }

            if (_registeredBehaviour != null)
            {
                try
                {
                    player.m_body.RemoveBehaviour(_registeredBehaviour);
                }
                catch
                {
                }
            }

            _registeredBehaviour = TowerCounterBehaviour.Instance;
            player.m_body.RegisterBehaviour(_registeredBehaviour);
        }

        [PauseMenuItemSetting]
        [MainMenuItemSetting]
        public static DisplayCounterToggle DisplayCounterMenu(object factory, GuiFormat format)
        {
            return new DisplayCounterToggle();
        }
    }

    public class DisplayCounterToggle : ITextToggle
    {
        public DisplayCounterToggle() : base(TowerCounterDisplay.Enabled)
        {
        }

        protected override string GetName()
        {
            return "Tower Counter";
        }

        protected override void OnToggle()
        {
            TowerCounterDisplay.Enabled = toggle;

            if (toggle)
            {
                TowerCounterBehaviour.ReloadState();
            }
        }
    }

    public class TowerCounterDisplay : Entity
    {
        private static readonly FieldInfo TimerDisplayPositionField = typeof(GameLoop).GetField(
            "TIMER_DISPLAY_POSITION",
            BindingFlags.Static | BindingFlags.NonPublic
        );

        private static TowerCounterDisplay _entity;

        public static bool Enabled = true;

        public static void EnsureAdded()
        {
            if (EntityManager.instance == null)
            {
                return;
            }

            if (_entity != null && _entity.IsAlive)
            {
                return;
            }

            _entity = new TowerCounterDisplay();
            EntityManager.instance.AddObject(_entity);
        }

        public override void Draw()
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

    public class TowerCounterBehaviour : IBodyCompBehaviour
    {
        private const int MinScreen = 1;
        private const int MaxScreen = 169;
        private const string StateFileName = "towercounter.state";

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

        private readonly string _statePath;

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
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _statePath = Path.Combine(assemblyDir, StateFileName);
            _locations = LoadLocations();
            LoadState();
        }

        public bool ExecuteBehaviour(BehaviourContext behaviourContext)
        {
            if (!TowerCounterDisplay.Enabled)
            {
                return true;
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

            UpdateAutoCount(screen, area, behaviourContext.BodyComp.IsOnGround);
            _previousKeyboardState = keyboardState;

            return true;
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
            try
            {
                if (!File.Exists(_statePath))
                {
                    return;
                }

                string[] lines = File.ReadAllLines(_statePath);

                if (lines.Length < 2)
                {
                    return;
                }

                int count;
                int entranceScreen;

                if (!int.TryParse(lines[0], out count) ||
                    !int.TryParse(lines[1], out entranceScreen))
                {
                    return;
                }

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
            catch
            {
            }
        }

        private void SaveState()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(_count.ToString());
                sb.AppendLine(_entranceScreen.ToString());
                File.WriteAllText(_statePath, sb.ToString());
            }
            catch
            {
            }
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
