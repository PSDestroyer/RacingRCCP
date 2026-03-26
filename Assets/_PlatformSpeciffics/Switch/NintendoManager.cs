
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


    }
#endif
}