using System;
namespace Control4Crestron
{
    public sealed class GatewayStatusEventArgs : EventArgs
    {
        public ushort Connected { get; set; }
        public ushort Busy { get; set; }
        public string Message { get; set; }
    }
    public sealed class LightStatusEventArgs : EventArgs
    {
        public ushort ItemId { get; set; }
        public ushort On { get; set; }
        public ushort Level { get; set; }
        public ushort Valid { get; set; }
        // 0 is unknown, 1 is on/off, 2 is dimmable.
        public ushort Dimming { get; set; }
    }
    public sealed class GatewayMessageEventArgs : EventArgs
    {
        public string Message { get; set; }
    }
    public sealed class ShadeStatusEventArgs : EventArgs
    {
        public ushort ItemId { get; set; }
        public ushort Position { get; set; }
        public ushort FullyOpen { get; set; }
        public ushort FullyClosed { get; set; }
        public ushort Valid { get; set; }
    }
    public sealed class AudioStatusEventArgs : EventArgs
    {
        public ushort RoomId { get; set; }
        public ushort Power { get; set; }
        public ushort PowerValid { get; set; }
        public ushort Volume { get; set; }
        public ushort VolumeValid { get; set; }
        public ushort Muted { get; set; }
        public ushort MuteValid { get; set; }
        public ushort Valid { get; set; }
    }
    public sealed class RoomDiscoveryEventArgs : EventArgs
    {
        public ushort RoomId { get; set; }
        public ushort Valid { get; set; }
        public string Json { get; set; }
    }
    public sealed class RoomBrowserEventArgs : EventArgs
    {
        public string RoomName { get; set; }
        public ushort SourcesValid { get; set; }
        public ushort HasWatch { get; set; }
        public ushort HasListen { get; set; }
        public ushort WatchCount { get; set; }
        public ushort ListenCount { get; set; }
    }
    public sealed class RoomSourceListsEventArgs : EventArgs
    {
        public string AudioJson { get; set; }
        public string VideoJson { get; set; }
        public ushort AudioPage { get; set; }
        public ushort VideoPage { get; set; }
        public ushort AudioPages { get; set; }
        public ushort VideoPages { get; set; }
    }
    public sealed class RoomActiveSourceEventArgs : EventArgs
    {
        public ushort RoomId { get; set; }
        public int AudioId { get; set; }
        public int VideoId { get; set; }
        public ushort AudioValid { get; set; }
        public ushort VideoValid { get; set; }
    }
    public sealed class RoomSelectedSlotsEventArgs : EventArgs
    {
        public ushort AudioIndex { get; set; }
        public ushort VideoIndex { get; set; }
        public ushort AudioValid { get; set; }
        public ushort VideoValid { get; set; }
    }
    public sealed class RoomCatalogEventArgs : EventArgs
    {
        public ushort RoomId { get; set; }
        public ushort Valid { get; set; }
        public string Kind { get; set; }
        public string Json { get; set; }
    }
    public sealed class RoomMediaPageEventArgs : EventArgs
    {
        public ushort SelectionAvailable { get; set; }
        public ushort Valid { get; set; }
        public ushort Partial { get; set; }
        public ushort Count { get; set; }
        public ushort Page { get; set; }
        public ushort Pages { get; set; }
        public string Kind { get; set; }
        public string Json { get; set; }
    }
    public sealed class RoomSourceSlotEventArgs : EventArgs
    {
        public ushort Video { get; set; }
        public ushort Index { get; set; }
        public ushort Available { get; set; }
        public string Name { get; set; }
    }
    public sealed class RoomTransportEventArgs : EventArgs
    {
        public ushort RoomId { get; set; }
        public ushort DiscoveryValid { get; set; }
        public ushort CommandCount { get; set; }
        public ushort MappedCount { get; set; }
        public ushort PlayAvailable { get; set; }
        public ushort PauseAvailable { get; set; }
        public ushort StopAvailable { get; set; }
        public string Json { get; set; }
    }
    public sealed class RoomSourceSlotsStatusEventArgs : EventArgs
    {
        public ushort AudioOverflow { get; set; }
        public ushort VideoOverflow { get; set; }
        public ushort AudioOverflowCount { get; set; }
        public ushort VideoOverflowCount { get; set; }
    }
}
