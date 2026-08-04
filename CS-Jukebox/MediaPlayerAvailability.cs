using System;
using System.Runtime.InteropServices;

namespace CS_Jukebox
{
    internal static class MediaPlayerAvailability
    {
        public static bool IsAvailable(out string error)
        {
            error = null;
            object player = null;

            try
            {
                Type playerType = Type.GetTypeFromProgID("WMPlayer.OCX");
                if (playerType == null)
                {
                    error = "The Windows Media Player component is not registered.";
                    return false;
                }

                player = Activator.CreateInstance(playerType);
                return player != null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (player != null && Marshal.IsComObject(player))
                {
                    try { Marshal.FinalReleaseComObject(player); } catch { }
                }
            }
        }
    }
}
