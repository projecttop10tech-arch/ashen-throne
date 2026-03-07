using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.Alliance
{
    /// <summary>
    /// Manages alliance membership, roles, research, and territory war state.
    /// All mutation operations call PlayFab for server-side validation.
    /// </summary>
    public class AllianceManager : MonoBehaviour
    {
        public const int MinMembers = 1;
        public const int MaxMembers = 50;
        public const int NameMaxLength = 20;
        public const int TagMaxLength = 5;

        public AllianceData CurrentAlliance { get; private set; }
        public AllianceMember PlayerMember { get; private set; }
        public bool IsInAlliance => CurrentAlliance != null;

        public event Action<AllianceData> OnAllianceJoined;
        public event Action OnAllianceLeft;
        public event Action<AllianceMember> OnMemberJoined;
        public event Action<string> OnMemberLeft;          // memberId
        public event Action<string, AllianceRole> OnMemberRoleChanged;

        private void Awake()
        {
            ServiceLocator.Register<AllianceManager>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<AllianceManager>();
        }

        /// <summary>
        /// Load alliance state from server data after authentication.
        /// </summary>
        public void LoadFromSaveData(AllianceSaveData save, string playerPlayFabId)
        {
            if (save == null || string.IsNullOrEmpty(save.AllianceId))
            {
                CurrentAlliance = null;
                PlayerMember = null;
                return;
            }

            CurrentAlliance = new AllianceData(save);
            PlayerMember = CurrentAlliance.Members.Find(m => m.PlayFabId == playerPlayFabId);
        }

        /// <summary>
        /// Validate alliance name and tag before calling server to create.
        /// </summary>
        public bool ValidateNewAlliance(string name, string tag, out string error)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > NameMaxLength)
            {
                error = $"Alliance name must be 1–{NameMaxLength} characters.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(tag) || tag.Length > TagMaxLength)
            {
                error = $"Alliance tag must be 1–{TagMaxLength} characters.";
                return false;
            }
            // Sanitize: only alphanumeric + spaces allowed
            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != ' ')
                {
                    error = "Alliance name may only contain letters, numbers, and spaces.";
                    return false;
                }
            }
            error = null;
            return true;
        }

        /// <summary>
        /// Returns true if the player has permission to perform a given action based on their role.
        /// </summary>
        public bool HasPermission(AllianceAction action)
        {
            if (PlayerMember == null) return false;
            return action switch
            {
                AllianceAction.InviteMember => PlayerMember.Role >= AllianceRole.Officer,
                AllianceAction.KickMember => PlayerMember.Role >= AllianceRole.Officer,
                AllianceAction.StartWar => PlayerMember.Role >= AllianceRole.CoLeader,
                AllianceAction.ManageRoles => PlayerMember.Role >= AllianceRole.CoLeader,
                AllianceAction.DisbandAlliance => PlayerMember.Role == AllianceRole.Leader,
                AllianceAction.EditAllianceInfo => PlayerMember.Role >= AllianceRole.CoLeader,
                AllianceAction.StartRally => PlayerMember.Role >= AllianceRole.Member,
                _ => false
            };
        }

        /// <summary>
        /// Get all members sorted by contribution (descending).
        /// </summary>
        public List<AllianceMember> GetMembersByContribution()
        {
            if (CurrentAlliance == null) return new List<AllianceMember>();
            var sorted = new List<AllianceMember>(CurrentAlliance.Members);
            sorted.Sort((a, b) => b.WeeklyContribution.CompareTo(a.WeeklyContribution));
            return sorted;
        }
    }

    public class AllianceData
    {
        public string AllianceId { get; }
        public string Name { get; set; }
        public string Tag { get; set; }
        public string LeaderPlayFabId { get; set; }
        public List<AllianceMember> Members { get; } = new();
        public int TerritoryCount { get; set; }
        public AllianceResearch Research { get; set; }
        public string Emblem { get; set; } // Emblem config JSON

        public AllianceData(AllianceSaveData save)
        {
            AllianceId = save.AllianceId;
            Name = save.Name;
            Tag = save.Tag;
            LeaderPlayFabId = save.LeaderPlayFabId;
            TerritoryCount = save.TerritoryCount;
            Emblem = save.Emblem;
            Research = save.Research ?? new AllianceResearch();
            if (save.Members != null) Members.AddRange(save.Members);
        }
    }

    [System.Serializable]
    public class AllianceMember
    {
        public string PlayFabId;
        public string DisplayName;
        public AllianceRole Role;
        public int PowerScore;
        public long WeeklyContribution;
        public long TotalContribution;
        public DateTime LastOnline;
    }

    [System.Serializable]
    public class AllianceSaveData
    {
        public string AllianceId;
        public string Name;
        public string Tag;
        public string LeaderPlayFabId;
        public int TerritoryCount;
        public string Emblem;
        public List<AllianceMember> Members;
        public AllianceResearch Research;
    }

    [System.Serializable]
    public class AllianceResearch
    {
        /// <summary>Research points invested per tree node (nodeId → points). Max per node defined in ResearchConfig.</summary>
        public Dictionary<string, int> NodeInvestments = new();
    }

    public enum AllianceRole
    {
        Member = 0,
        Officer = 1,
        CoLeader = 2,
        Leader = 3
    }

    public enum AllianceAction
    {
        InviteMember,
        KickMember,
        StartWar,
        ManageRoles,
        DisbandAlliance,
        EditAllianceInfo,
        StartRally
    }

    // --- Events ---
    public readonly struct AllianceMemberJoinedEvent { public readonly string AllianceId; public readonly string MemberId; public AllianceMemberJoinedEvent(string a, string m) { AllianceId = a; MemberId = m; } }
    public readonly struct AllianceMemberLeftEvent { public readonly string AllianceId; public readonly string MemberId; public AllianceMemberLeftEvent(string a, string m) { AllianceId = a; MemberId = m; } }
}
