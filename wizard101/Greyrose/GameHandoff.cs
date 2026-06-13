using System;

namespace Greyrose
{
    /// <summary>
    /// Bridges login character-select to the game server's new TCP session.
    /// </summary>
    static class GameHandoff
    {
        static readonly object Lock = new();
        static long? _characterId;
        static long? _charGid;
        static long? _userGid;
        static DateTime _registeredAt;

        public static void Register(long charGid, long characterId, long userGid)
        {
            lock (Lock)
            {
                _charGid = charGid;
                _characterId = characterId;
                _userGid = userGid;
                _registeredAt = DateTime.UtcNow;
            }
        }

        public static bool TryApply(ClientSession session)
        {
            lock (Lock)
            {
                if (!_characterId.HasValue)
                    return false;
                if ((DateTime.UtcNow - _registeredAt).TotalSeconds > 120)
                {
                    ClearLocked();
                    return false;
                }

                session.SelectedCharacterId = _characterId;
                session.AccountUserGid = _userGid;
                ServerLog.WriteLine("GS: handoff applied — char id {0}, GID {1}, user GID {2}.",
                    _characterId.Value, _charGid ?? 0, _userGid ?? 0);
                ClearLocked();
                return true;
            }
        }

        static void ClearLocked()
        {
            _characterId = null;
            _charGid = null;
            _userGid = null;
        }
    }
}
