namespace Greyrose.Data
{
    public class AccountRecord
    {
        public long Id { get; set; }
        public long UserGid { get; set; }
        public string Username { get; set; } = "";
        public string PassKey { get; set; } = "";
        public int PurchasedSlots { get; set; }
    }

    public class CharacterRecord
    {
        public long Id { get; set; }
        public long AccountId { get; set; }
        public long CharGid { get; set; }
        public string Name { get; set; } = "";
        public int Slot { get; set; }
        public string ZoneName { get; set; } = "WizardCity/WC_Ravenwood";
        public long ZoneGid { get; set; }
        public string Location { get; set; } = "2572,4376,-28,5.55";
        public string CharacterInfoHex { get; set; } = "";
    }

    public class PlayerStateRecord
    {
        public long CharacterId { get; set; }
        public float X { get; set; } = 2572;
        public float Y { get; set; } = 4376;
        public float Z { get; set; } = -28;
        public float Rot { get; set; } = 5.55f;
        public ushort MarkerX { get; set; }
        public ushort MarkerY { get; set; }
        public ushort MarkerZ { get; set; }
        public byte MarkerRot { get; set; }
        public string LoginBlobHex { get; set; } = "";
        public string ZoneBlobHex { get; set; }
    }
}
