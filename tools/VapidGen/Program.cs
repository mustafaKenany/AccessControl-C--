// One-off VAPID key generator for web push. Run once; paste the keys into the VPS
// appsettings.json under "WebPush", then this tool is no longer needed.
var keys = WebPush.VapidHelper.GenerateVapidKeys();
Console.WriteLine("VAPID_PUBLIC=" + keys.PublicKey);
Console.WriteLine("VAPID_PRIVATE=" + keys.PrivateKey);
