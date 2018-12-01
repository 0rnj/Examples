using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Steamworks;

public class PremadeLobbyManager : MonoBehaviour
{
    public GameObject[] TeammatePads;
    public TextMesh[] TeammateNames;
    public Customizer[] TeammateCustomizers;
    public Image[] TeammateAvatars;
    public Sprite DefaultSprite;

    public bool CreatingPremadeTeamLobby;
    public CSteamID teambuilderLobbyId;

    private Callback<LobbyChatMsg_t> m_LobbyChatMessage;
    private Coroutine _pingCoroutine;

    private SteamNetworkManager _steamManager;


    private void Start()
    {
        _steamManager = SteamNetworkManager.Instance;
        _steamManager.premadeLobbyRef = this;

        if (SteamManager.Initialized)
        {
            m_LobbyChatMessage = Callback<LobbyChatMsg_t>.Create(OnLobbyChatMessage);
        }
    }

    public void CreatePremadeTeamLobby()
    {
        if (C.LOG_INVITE) Debug.LogWarning("Creating premade team lobby");
        if (teambuilderLobbyId == CSteamID.Nil)
        {
            CreatingPremadeTeamLobby = true;
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeInvisible, C.MAX_PLAYERS / 2);
        }
        else
        {
            _steamManager.UNETServerController.InviteFriendsToLobby(teambuilderLobbyId);
        }
    }

    /// <summary>
    /// Used for:
    /// 1. For host to invite friends to premade team
    /// 2. For players to share customization data to display it on each others' screens
    /// </summary>
    public void OnLobbyEntered(LobbyEnter_t pCallback)
    {
        if (C.LOG_INVITE) Debug.Log("Premade lobby: OnLobbyEntered");

        teambuilderLobbyId = new CSteamID(pCallback.m_ulSteamIDLobby);

        if (IsLobbyOwner())
        {
            CreatingPremadeTeamLobby = false;
            SteamMatchmaking.SetLobbyData(teambuilderLobbyId, "game", SteamNetworkManager.PREMADE_ID);
            _steamManager.UNETServerController.InviteFriendsToLobby(teambuilderLobbyId);
            _pingCoroutine = StartCoroutine(PingChat());
        }
        else
        {
            SteamNetworkManager.Instance.Invited = false;
        }

        PostCustomizationProfile();
    }

    public void OnLobbyDataUpdate(LobbyDataUpdate_t pCallback)
    {
        if (C.LOG_INVITE) Debug.Log("Premade lobby: OnLobbyDataUpdate");

        CheckLock();
        
        /// Lobby data update
        if (pCallback.m_ulSteamIDMember == pCallback.m_ulSteamIDLobby)
        {
            string foundLobby = SteamMatchmaking.GetLobbyData(teambuilderLobbyId, "foundLobby");
            if (foundLobby != "")
            {
                if (C.LOG_INVITE) Debug.Log("Game lobby found, jumping to it");

                /// Jump to found lobby
                var lobbyId = new CSteamID(ulong.Parse(foundLobby));

                /// Not for owner because he's already there at this moment
                if (!IsLobbyOwner())
                {
                    SteamNetworkManager.Instance.JoinLobby(lobbyId);
                    MainMenuManager.Instance.ProceedToLobby();
                }
            }
        }

        /// Member data update
        else
        {
            UpdateTeammatesPresence();
        }
    }

    public void OnLobbyChatUpdate(LobbyChatUpdate_t pCallback)
    {
        if (C.LOG_INVITE) Debug.Log("Premade lobby: OnLobbyChatUpdate");

        UpdateTeammatesPresence();
        CheckLock();

        /// If someone left
        if (pCallback.m_ulSteamIDLobby == teambuilderLobbyId.m_SteamID
            && (pCallback.m_rgfChatMemberStateChange == (uint)EChatMemberStateChange.k_EChatMemberStateChangeLeft
              || pCallback.m_rgfChatMemberStateChange == (uint)EChatMemberStateChange.k_EChatMemberStateChangeDisconnected))
        {
            if (C.LOG_INVITE) Debug.LogWarning(SteamFriends.GetFriendPersonaName(new CSteamID(pCallback.m_ulSteamIDUserChanged)) + " left lobby");

            /// Leave lobby if left only this player
            if (SteamMatchmaking.GetNumLobbyMembers(teambuilderLobbyId) <= 1)
                LeaveTeambuilderLobby();

            /// Update 'premade' message
            var snm = SteamNetworkManager.Instance;
            if (snm.steamLobbyId != CSteamID.Nil)
                SteamMatchmaking.SetLobbyMemberData(snm.steamLobbyId, "premade", GeneratePremadeMessage());

            /// If became new lobby owner - start pinging chat
            if (IsLobbyOwner() && _pingCoroutine == null)
            {
                _pingCoroutine = StartCoroutine(PingChat());
            }
        }

        /// If user enters lobby
        else if (pCallback.m_rgfChatMemberStateChange == (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered
            && pCallback.m_ulSteamIDLobby == teambuilderLobbyId.m_SteamID)
        {
            if (C.LOG_INVITE) Debug.LogWarning(SteamFriends.GetFriendPersonaName(new CSteamID(pCallback.m_ulSteamIDUserChanged)) + " joined lobby");
        }
    }

    private void OnLobbyChatMessage(LobbyChatMsg_t pCallback)
    {
        if (C.LOG_INVITE) Debug.Log("Teambuilder chat ping");
    }

    /// <summary>
    /// Used to make lobby persist.
    /// Lobby shuts down if no data sent within it for a while
    /// </summary>
    IEnumerator PingChat()
    {
        var msg = System.Text.Encoding.ASCII.GetBytes("ping");
        var length = System.Text.Encoding.ASCII.GetByteCount("ping");
        while (teambuilderLobbyId != null)
        {
            var ret = SteamMatchmaking.SendLobbyChatMsg(teambuilderLobbyId, msg, length);
            if (C.LOG_INVITE) Debug.Log("Sending teambuilder ping success: " + ret);
            yield return new WaitForSeconds(5f);
        }
        yield break;
    }

    /// <summary>
    /// Setup customization profile to display to other players in premade lobby.
    /// Can be used as "refresh" when returning from arena to main menu
    /// </summary>
    public void PostCustomizationProfile()
    {
        if (teambuilderLobbyId != null)
        {
            var preset = DataManager.Instance.GetCustomizationProfile();
            if (C.LOG_INVITE) Debug.Log("Setting up my customization profile: " + preset);
            SteamMatchmaking.SetLobbyMemberData(teambuilderLobbyId, "preset", preset);
        }
    }

    public void InviteTeamToFoundLobby(CSteamID lobbyId)
    {
        if (IsLobbyOwner())
        {
            if (C.LOG_INVITE) Debug.Log("Inviting premade team to found game lobby");
            SteamMatchmaking.SetLobbyData(teambuilderLobbyId, "foundLobby", lobbyId.m_SteamID.ToString());
        }
    }

    /// <summary>
    /// Generates string message with premade team members' IDs to be posted in game lobby.
    /// Necessary for host to process premade teams
    /// </summary>
    public string GeneratePremadeMessage()
    {
        string msg = "";
        for (int i = 0; i < SteamMatchmaking.GetNumLobbyMembers(teambuilderLobbyId); i++)
        {
            msg += SteamMatchmaking.GetLobbyMemberByIndex(teambuilderLobbyId, i).m_SteamID.ToString();
            msg += ".";
        }

        return msg;
    }

    /// <summary>
    /// Show / hide 'leave group' and lock / unlock play
    /// </summary>
    private void CheckLock()
    {
        if (MainMenuManager.Instance != null)
            MainMenuManager.Instance.MenuPlayLock(
                SteamMatchmaking.GetNumLobbyMembers(teambuilderLobbyId) > 1,
                IsLobbyOwner());
    }

    /// <summary>
    /// Updates players' gameobject avatars on main menu's scene
    /// </summary>
    void UpdateTeammatesPresence()
    {
        if (C.LOG_INVITE) Debug.Log("Updating teammates presence");

        /// Cleanup
        FlushTeammatesPresence();

        /// Placing
        var membersNum = SteamMatchmaking.GetNumLobbyMembers(teambuilderLobbyId);
        int index = 0;  /// using 'index' variable to access Teammate* arrays without running out of borders
        for (int i = 0; i < membersNum; i++)
        {
            var member = SteamMatchmaking.GetLobbyMemberByIndex(teambuilderLobbyId, i);
            if (member != SteamUser.GetSteamID())  /// if not our player
            {
                if (C.LOG_INVITE) Debug.Log("Updating team-builder lobby data on player: " + SteamFriends.GetFriendPersonaName(member));
                
                /// Get name and avatar
                TeammateNames[index].text = SteamFriends.GetFriendPersonaName(member);
                SteamNetworkManager.Instance.GetUserData(member, (data) =>
                {
                    /// Steam avatars uploaded 'inverted', needed to rotate
                    TeammateAvatars[index].rectTransform.rotation = Quaternion.Euler(new Vector3(0f, 180f, 180f));
                    TeammateAvatars[index].sprite = data.Avatar;
                    TeammateAvatars[index].transform.parent.GetChild(1).gameObject.GetComponentInChildren<ImageState>().SetState(true);
                });

                /// Get customization profile
                int[] preset = new int[4];
                var strPreset = SteamMatchmaking.GetLobbyMemberData(teambuilderLobbyId, member, "preset");
                if (C.LOG_INVITE) Debug.Log("Got " + SteamFriends.GetFriendPersonaName(member) + "'s customization message: " + strPreset);

                /// Place avatar gameobject
                if (strPreset != "")
                    TeammatePads[index].SetActive(true);

                /// Parse and apply customization
                for (int j = 0; j < preset.Length; j++)
                {
                    if (strPreset != "")
                    {
                        var sp = strPreset.Substring(j * 5 + 1, 4);
                        if (C.LOG_INVITE) Debug.Log("Preset #" + (j + 1).ToString() + ": " + sp);
                        preset[j] = int.Parse(sp);
                    }
                    else TeammatePads[index].SetActive(false);
                }
                if (C.LOG_INVITE) Debug.Log("Customization profile of " + SteamFriends.GetFriendPersonaName(member) + " is: " + Utility.LogArray(preset));
                TeammateCustomizers[index].ApplyCustomization(preset);

                index++;
            }
        }
    }

    void FlushTeammatesPresence()
    {
        for (int i = 0; i < TeammatePads.Length; i++)
        {
            TeammatePads[i].SetActive(false);
            TeammateAvatars[i].rectTransform.rotation = Quaternion.Euler(Vector3.zero);
            TeammateAvatars[i].sprite = DefaultSprite;
            TeammateAvatars[i].transform.parent.GetChild(1).gameObject.GetComponentInChildren<ImageState>().SetState(false);
        }
    }

    /// <summary>
    /// Returns 'true' if this user is owner of premade lobby
    /// </summary>
    public bool IsLobbyOwner()
    {
        return SteamMatchmaking.GetLobbyOwner(teambuilderLobbyId) == SteamUser.GetSteamID();
    }

    public void LeaveEmptyTeambuilderLobby()
    {
        if (teambuilderLobbyId != CSteamID.Nil && 
            SteamMatchmaking.GetNumLobbyMembers(teambuilderLobbyId) <= 1)
        {
            LeaveTeambuilderLobby();
        }
    }

    public void LeaveTeambuilderLobby()
    {
        if (teambuilderLobbyId != CSteamID.Nil)
        {
            StopAllCoroutines();

            SteamMatchmaking.LeaveLobby(teambuilderLobbyId);
            SteamNetworkManager.Instance.premadeLobbyId = CSteamID.Nil;
            teambuilderLobbyId = CSteamID.Nil;

            FlushTeammatesPresence();
            MainMenuManager.Instance.MenuPlayLock(false);
        }
    }
}
