
using UnityEngine;

namespace _Assets._PlatformSpeciffics.Switch
{
#if UNITY_SWITCH && !UNITY_EDITOR
    public static class NintendoManager 
    {

        private static nn.account.Uid userId;
#pragma warning disable 0414
        private static nn.fs.FileHandle fileHandle = new nn.fs.FileHandle();
#pragma warning restore 0414

        

        public static void Initialize()
        {
            Debug.LogError("!!! Start Initialize NXScript !!!");
            
            nn.account.Account.Initialize();
            nn.account.UserHandle userHandle = new nn.account.UserHandle();
            
            
            
            if (!nn.account.Account.TryOpenPreselectedUser(ref userHandle))
            {
                nn.Nn.Abort("Failed to open preselected user.");
            }
            
            nn.Result result = nn.account.Account.GetUserId(ref userId, userHandle);
            result.abortUnlessSuccess();
            
            //result = nn.fs.SaveData.Mount(mountName, userId);
            //result.abortUnlessSuccess();

            //nn.hid.Npad.Initialize();
            //nn.hid.Npad.SetSupportedStyleSet(nn.hid.NpadStyle.Handheld | nn.hid.NpadStyle.JoyDual);
            //nn.hid.Npad.SetSupportedIdType(npadIds);
            //npadState = new nn.hid.NpadState();
        }
        
        
        

        public static nn.account.Uid GetUserID()
        {
            Debug.LogError($"!!! USER ID : {userId} !!!");
            return userId;
        }

        /// <summary>
        /// Returns the nickname of the currently opened Nintendo user.
        /// </summary>
        public static string GetNickname()
        {
            nn.account.Nickname nickname = new nn.account.Nickname();
            nn.Result result = nn.account.Account.GetNickname(ref nickname, userId);

            if (!result.IsSuccess())
            {
                Debug.LogError($"Failed to read Nintendo user nickname: {result}");
                return string.Empty;
            }

            string value = nickname.name;
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            int nullTerminatorIndex = value.IndexOf('\0');
            if (nullTerminatorIndex >= 0)
                value = value.Substring(0, nullTerminatorIndex);

            return value.Trim();
        }

        /// <summary>
        /// Returns the highest-priority language supported by the application,
        /// according to the language preferences configured on the console.
        /// </summary>
        public static string GetDesiredLanguage()
        {
            string desiredLanguage = nn.oe.Language.GetDesired();

            if (string.IsNullOrEmpty(desiredLanguage))
                return string.Empty;

            desiredLanguage = desiredLanguage.Trim();
            int nullTerminatorIndex = desiredLanguage.IndexOf('\0');

            if (nullTerminatorIndex >= 0)
                desiredLanguage = desiredLanguage.Substring(0, nullTerminatorIndex);

            return desiredLanguage;
        }

    }
#endif
}
