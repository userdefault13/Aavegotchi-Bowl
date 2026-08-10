using UnityEngine;
using RetroBowl.Data;
using RetroBowl.Managers;

namespace RetroBowl.App
{
    public enum CareerTutorialPhase
    {
        NotStarted = 0,
        HomeWelcome = 1,
        NeedFrontOffice = 2,
        FrontOfficeTip = 3,
        RosterNudge = 4,
        RosterTip = 5,
        ProfileTip = 6,
        ControlsBasics = 7,
        TrainingFacility = 8,
        Complete = 9
    }

    [System.Serializable]
    public struct SaveSlotMeta
    {
        public bool exists;
        public string teamCity;
        public string teamName;
        public string coachFirst;
        public string coachLast;
        public int week;
        public int season;
        public int wins;
        public int losses;
        public float morale;

        public string TeamLabel =>
            string.IsNullOrEmpty(teamCity) ? "" : $"{teamCity} {teamName}".Trim();

        public string RecordLabel => $"{wins}-{losses}";

        public string CoachLabel =>
            string.IsNullOrEmpty(coachFirst) && string.IsNullOrEmpty(coachLast)
                ? ""
                : $"{coachFirst} {coachLast}".Trim();

        public string SummaryLine
        {
            get
            {
                if (!exists) return "";
                string team = string.IsNullOrEmpty(TeamLabel) ? "FRANCHISE" : TeamLabel.ToUpperInvariant();
                return $"{team}  {RecordLabel}  W{week}  Y{season}";
            }
        }
    }

    /// <summary>Multi-slot PlayerPrefs career save (5 slots) + tutorial / FO meta.</summary>
    public class SaveService : MonoBehaviour
    {
        public static SaveService Instance { get; private set; }

        public const int SlotCount = 5;

        const string KeyActive = "agb_active_slot";
        const string KeyLegacyMigrated = "agb_slots_migrated";

        // Legacy single-slot keys (migrated into slot 0 once).
        const string LegacyHasSave = "agb_has_save";
        const string LegacyWeek = "agb_week";
        const string LegacySeason = "agb_season";
        const string LegacyWins = "agb_wins";
        const string LegacyLosses = "agb_losses";
        const string LegacyMorale = "agb_morale";
        const string LegacyTeamCity = "agb_team_city";
        const string LegacyTeamName = "agb_team_name";
        const string LegacyChoseTeam = "agb_chose_team";

        public int ActiveSlot { get; private set; } = -1;
        public bool HasActiveSlot => ActiveSlot >= 0 && ActiveSlot < SlotCount;

        public bool HasSave => HasActiveSlot && SlotExists(ActiveSlot);
        public bool HasChosenTeam => HasActiveSlot && GetInt(ActiveSlot, "chose") == 1;

        public string CoachFirst => HasActiveSlot ? GetString(ActiveSlot, "coach_first", "Coach") : "Coach";
        public string CoachLast => HasActiveSlot ? GetString(ActiveSlot, "coach_last", "") : "";
        public int FaceId => HasActiveSlot ? GetInt(ActiveSlot, "face") : 0;
        public int Credits => HasActiveSlot ? GetInt(ActiveSlot, "credits", 3) : 3;
        public int FansPercent => HasActiveSlot ? Mathf.Clamp(GetInt(ActiveSlot, "fans", 40), 0, 100) : 40;
        public int SalaryCapM => HasActiveSlot ? GetInt(ActiveSlot, "cap_m", 50) : 50;
        public int SalaryCapMaxM => 150;
        public int StadiumLevel => HasActiveSlot ? GetInt(ActiveSlot, "stadium", 1) : 1;
        public int TrainingLevel => HasActiveSlot ? GetInt(ActiveSlot, "training", 2) : 2;
        public int RehabLevel => HasActiveSlot ? GetInt(ActiveSlot, "rehab", 2) : 2;
        public CareerTutorialPhase TutorialPhase
        {
            get => HasActiveSlot
                ? (CareerTutorialPhase)GetInt(ActiveSlot, "tut", (int)CareerTutorialPhase.NotStarted)
                : CareerTutorialPhase.NotStarted;
            private set
            {
                if (!HasActiveSlot) return;
                SetInt(ActiveSlot, "tut", (int)value);
                PlayerPrefs.Save();
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            MigrateLegacyIfNeeded();
            ActiveSlot = PlayerPrefs.GetInt(KeyActive, -1);
            if (ActiveSlot < -1 || ActiveSlot >= SlotCount)
                ActiveSlot = -1;
        }

        static string Pref(int slot, string key) => $"agb_s{slot}_{key}";

        int GetInt(int slot, string key, int def = 0) => PlayerPrefs.GetInt(Pref(slot, key), def);
        void SetInt(int slot, string key, int v) => PlayerPrefs.SetInt(Pref(slot, key), v);
        float GetFloat(int slot, string key, float def = 0f) => PlayerPrefs.GetFloat(Pref(slot, key), def);
        void SetFloat(int slot, string key, float v) => PlayerPrefs.SetFloat(Pref(slot, key), v);
        string GetString(int slot, string key, string def = "") => PlayerPrefs.GetString(Pref(slot, key), def);
        void SetString(int slot, string key, string v) => PlayerPrefs.SetString(Pref(slot, key), v ?? "");

        public bool SlotExists(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return false;
            return GetInt(slot, "has") == 1;
        }

        public SaveSlotMeta GetSlotMeta(int slot)
        {
            var m = new SaveSlotMeta();
            if (slot < 0 || slot >= SlotCount) return m;
            m.exists = SlotExists(slot);
            if (!m.exists) return m;
            m.teamCity = GetString(slot, "city");
            m.teamName = GetString(slot, "name");
            m.coachFirst = GetString(slot, "coach_first");
            m.coachLast = GetString(slot, "coach_last");
            m.week = GetInt(slot, "week", 1);
            m.season = GetInt(slot, "season", 1);
            m.wins = GetInt(slot, "wins");
            m.losses = GetInt(slot, "losses");
            m.morale = GetFloat(slot, "morale", 70f);
            return m;
        }

        public void SetActiveSlot(int slot)
        {
            ActiveSlot = slot;
            PlayerPrefs.SetInt(KeyActive, slot);
            PlayerPrefs.Save();
        }

        /// <summary>Bind empty slot for NEW GAME — resets live managers, clears slot prefs.</summary>
        public void BeginNewGame(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return;
            ClearSlotPrefs(slot);
            SetActiveSlot(slot);
            ResetLiveSeason();
            if (TeamManager.Instance != null)
                TeamManager.Instance.playerTeam = null;
            TutorialPhase = CareerTutorialPhase.NotStarted;
            SetInt(slot, "credits", 3);
            SetInt(slot, "fans", 40);
            SetInt(slot, "cap_m", 50);
            SetInt(slot, "stadium", 1);
            SetInt(slot, "training", 2);
            SetInt(slot, "rehab", 2);
            PlayerPrefs.Save();
        }

        public void LoadCareer(int slot)
        {
            if (slot < 0 || slot >= SlotCount || !SlotExists(slot)) return;
            SetActiveSlot(slot);
            LoadCareer();
        }

        public void LoadCareer()
        {
            if (!HasActiveSlot || !HasSave) return;
            var s = SeasonManager.Instance;
            var t = TeamManager.Instance;
            int slot = ActiveSlot;
            if (s != null)
            {
                s.currentWeek = GetInt(slot, "week", 1);
                s.currentSeason = GetInt(slot, "season", 1);
                s.playerWins = GetInt(slot, "wins");
                s.playerLosses = GetInt(slot, "losses");
                s.teamMorale = GetFloat(slot, "morale", 70f);
                if (s.schedule == null || s.schedule.Count == 0)
                    s.GenerateSchedule();
            }
            if (t != null && HasChosenTeam)
            {
                string city = GetString(slot, "city", "New York");
                string name = GetString(slot, "name", "Thunder");
                if (t.playerTeam == null)
                {
                    t.playerTeam = new TeamData(city, name,
                        new Color(0.15f, 0.45f, 0.9f), Color.white);
                    t.playerTeam.GenerateRoster();
                }
                else
                {
                    t.playerTeam.cityName = city;
                    t.playerTeam.teamName = name;
                    if (t.playerTeam.roster == null || t.playerTeam.roster.Count == 0)
                        t.playerTeam.GenerateRoster();
                }
            }
        }

        public void SaveCareer()
        {
            if (!HasActiveSlot) return;
            // Don't persist empty onboarding slots until a team is chosen.
            if (!HasChosenTeam && !SlotExists(ActiveSlot)) return;

            var s = SeasonManager.Instance;
            var t = TeamManager.Instance;
            int slot = ActiveSlot;
            if (s != null)
            {
                SetInt(slot, "week", s.currentWeek);
                SetInt(slot, "season", s.currentSeason);
                SetInt(slot, "wins", s.playerWins);
                SetInt(slot, "losses", s.playerLosses);
                SetFloat(slot, "morale", s.teamMorale);
            }
            if (t != null && t.playerTeam != null)
            {
                SetString(slot, "city", t.playerTeam.cityName);
                SetString(slot, "name", t.playerTeam.teamName);
                if (HasChosenTeam || !string.IsNullOrEmpty(t.playerTeam.teamName))
                    SetInt(slot, "chose", 1);
            }
            SetInt(slot, "has", 1);
            PlayerPrefs.Save();
        }

        public void SaveCoachProfile(string first, string last, int faceId, string favCity, string favName, bool startWithFavorite)
        {
            if (!HasActiveSlot) return;
            int slot = ActiveSlot;
            SetString(slot, "coach_first", first ?? "Coach");
            SetString(slot, "coach_last", last ?? "");
            SetInt(slot, "face", faceId);
            SetString(slot, "fav_city", favCity ?? "");
            SetString(slot, "fav_name", favName ?? "");
            SetInt(slot, "start_fav", startWithFavorite ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void MarkTeamChosen(string city, string name)
        {
            if (!HasActiveSlot) return;
            int slot = ActiveSlot;
            SetInt(slot, "chose", 1);
            SetString(slot, "city", city ?? "New York");
            SetString(slot, "name", name ?? "Thunder");
            SetInt(slot, "has", 1);
            if (TutorialPhase == CareerTutorialPhase.NotStarted)
                TutorialPhase = CareerTutorialPhase.HomeWelcome;
            PlayerPrefs.Save();
        }

        public void ClearSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return;
            ClearSlotPrefs(slot);
            if (ActiveSlot == slot)
            {
                ActiveSlot = -1;
                PlayerPrefs.SetInt(KeyActive, -1);
                ResetLiveSeason();
                if (TeamManager.Instance != null)
                    TeamManager.Instance.playerTeam = null;
            }
            PlayerPrefs.Save();
        }

        public void ClearSave()
        {
            if (HasActiveSlot)
                ClearSlot(ActiveSlot);
        }

        public void AdvanceTutorial(CareerTutorialPhase phase)
        {
            if (!HasActiveSlot) return;
            if ((int)phase >= (int)TutorialPhase)
                TutorialPhase = phase;
        }

        public void SetTutorial(CareerTutorialPhase phase)
        {
            TutorialPhase = phase;
        }

        public void AddCredits(int amount)
        {
            if (!HasActiveSlot || amount <= 0) return;
            SetInt(ActiveSlot, "credits", Credits + amount);
            PlayerPrefs.Save();
        }

        public bool TrySpendCredits(int cost)
        {
            if (!HasActiveSlot || Credits < cost) return false;
            SetInt(ActiveSlot, "credits", Credits - cost);
            PlayerPrefs.Save();
            return true;
        }

        public void UpgradeStadium()
        {
            if (!TrySpendCredits(2)) return;
            SetInt(ActiveSlot, "stadium", Mathf.Min(5, StadiumLevel + 1));
            PlayerPrefs.Save();
        }

        public void UpgradeTraining()
        {
            if (!TrySpendCredits(3)) return;
            SetInt(ActiveSlot, "training", Mathf.Min(5, TrainingLevel + 1));
            PlayerPrefs.Save();
        }

        public void UpgradeRehab()
        {
            if (!TrySpendCredits(3)) return;
            SetInt(ActiveSlot, "rehab", Mathf.Min(5, RehabLevel + 1));
            PlayerPrefs.Save();
        }

        public void BumpSalaryCap()
        {
            if (!TrySpendCredits(1)) return;
            SetInt(ActiveSlot, "cap_m", Mathf.Min(SalaryCapMaxM, SalaryCapM + 10));
            PlayerPrefs.Save();
        }

        void ClearSlotPrefs(int slot)
        {
            string[] keys =
            {
                "has", "chose", "week", "season", "wins", "losses", "morale",
                "city", "name", "coach_first", "coach_last", "face",
                "fav_city", "fav_name", "start_fav", "tut",
                "credits", "fans", "cap_m", "stadium", "training", "rehab"
            };
            foreach (var k in keys)
                PlayerPrefs.DeleteKey(Pref(slot, k));
        }

        void ResetLiveSeason()
        {
            var s = SeasonManager.Instance;
            if (s == null) return;
            s.currentWeek = 1;
            s.currentSeason = 1;
            s.playerWins = 0;
            s.playerLosses = 0;
            s.teamMorale = 70f;
            s.GenerateSchedule();
        }

        void MigrateLegacyIfNeeded()
        {
            if (PlayerPrefs.GetInt(KeyLegacyMigrated, 0) == 1) return;
            if (PlayerPrefs.GetInt(LegacyHasSave, 0) == 1 && !SlotExists(0))
            {
                SetInt(0, "has", 1);
                SetInt(0, "chose", PlayerPrefs.GetInt(LegacyChoseTeam, 0));
                SetInt(0, "week", PlayerPrefs.GetInt(LegacyWeek, 1));
                SetInt(0, "season", PlayerPrefs.GetInt(LegacySeason, 1));
                SetInt(0, "wins", PlayerPrefs.GetInt(LegacyWins, 0));
                SetInt(0, "losses", PlayerPrefs.GetInt(LegacyLosses, 0));
                SetFloat(0, "morale", PlayerPrefs.GetFloat(LegacyMorale, 70f));
                SetString(0, "city", PlayerPrefs.GetString(LegacyTeamCity, "New York"));
                SetString(0, "name", PlayerPrefs.GetString(LegacyTeamName, "Thunder"));
                SetInt(0, "credits", 3);
                SetInt(0, "fans", 40);
                SetInt(0, "cap_m", 50);
                SetInt(0, "stadium", 1);
                SetInt(0, "training", 2);
                SetInt(0, "rehab", 2);
                SetInt(0, "tut", (int)CareerTutorialPhase.Complete);
                PlayerPrefs.SetInt(KeyActive, 0);
            }
            PlayerPrefs.SetInt(KeyLegacyMigrated, 1);
            PlayerPrefs.Save();
        }
    }
}
