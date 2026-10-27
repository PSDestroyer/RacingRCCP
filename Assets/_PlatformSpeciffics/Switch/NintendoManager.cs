
using UnityEngine;

namespace _Assets._PlatformSpeciffics.Switch
{
#if UNITY_SWITCH && !UNITY_EDITOR
    public static class NintendoManager 
    {

        private static nn.account.Uid userId;
        private static bool initialized;
        private static bool profileImageLoaded;
        private static Texture2D profileImage;
#pragma warning disable 0414
        private static nn.fs.FileHandle fileHandle = new nn.fs.FileHandle();
#pragma warning restore 0414

        

        public static void Initialize()
        {
            if (initialized) return;
            Debug.LogError("!!! Start Initialize NXScript !!!");
            
            nn.account.Account.Initialize();
            nn.account.UserHandle userHandle = new nn.account.UserHandle();
            
            
            
            if (!nn.account.Account.TryOpenPreselectedUser(ref userHandle))
            {
                nn.Nn.Abort("Failed to open preselected user.");
            }
            
            nn.Result result = nn.account.Account.GetUserId(ref userId, userHandle);
            result.abortUnlessSuccess();
            initialized = true;
            Application.quitting += ReleaseProfileImage;
            
            //result = nn.fs.SaveData.Mount(mountName, userId);
            //result.abortUnlessSuccess();

            //nn.hid.Npad.Initialize();
            //nn.hid.Npad.SetSupportedStyleSet(nn.hid.NpadStyle.Handheld | nn.hid.NpadStyle.JoyDual);
            //nn.hid.Npad.SetSupportedIdType(npadIds);
            //npadState = new nn.hid.NpadState();
        }
        
        
        

        public static nn.account.Uid GetUserID()
        {
            Initialize();
            Debug.LogError($"!!! USER ID : {userId} !!!");
            return userId;
        }

        public static bool TryGetNickname(out string nickname)
        {
            Initialize();
            var value = new nn.account.Nickname();
            var result = nn.account.Account.GetNickname(ref value, userId);
            nickname = result.IsSuccess() ? value.name : null;
            return !string.IsNullOrWhiteSpace(nickname);
        }

        public static Texture2D GetProfileImage()
        {
            if (profileImageLoaded) return profileImage;
            Initialize();
            profileImageLoaded = true;
            // One bounded read per application, shared by all menu headers.
            var bytes = new byte[128 * 1024];
            long size = 0;
            var result = nn.account.Account.LoadProfileImage(ref size, bytes, bytes.LongLength, userId);
            if (!result.IsSuccess() || size <= 0 || size > bytes.LongLength) return null;
            System.Array.Resize(ref bytes, (int)size);
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false)
            {
                name = "Nintendo User Profile", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            if (!ImageConversion.LoadImage(texture, bytes, true))
            {
                Object.Destroy(texture);
                return null;
            }
            profileImage = texture;
            return profileImage;
        }

        private static void ReleaseProfileImage()
        {
            if (profileImage != null) Object.Destroy(profileImage);
            profileImage = null;
        }


    }
#endif
}
