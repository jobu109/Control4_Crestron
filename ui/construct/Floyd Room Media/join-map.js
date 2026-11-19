// UI join map. Account/Director credentials and opaque selectors never belong here.
// Input/output sharing is limited to matching functions; module-only arrays use JSON.
window.ROOM_MEDIA_CONFIG = {
  enabled: true,
  "host": "192.168.1.169",
  "ipId": "0x03",
  "roomId": 13,
  "send": {
    "sourceId": "1",
    "mediaRequest": "2",
    "volume": "1",
    "mediaPage": "2",
    "audioPage": "3",
    "videoPage": "4",
    "selectAudio": "1",
    "selectVideo": "2",
    "muteOn": "3",
    "muteOff": "4",
    "off": "5",
    "shuffle": "6",
    "transport": {
      "Watch": "30",
      "Control4": "31",
      "Listen": "32",
      "List": "33",
      "Custom_1": "34",
      "Custom_2": "35",
      "Custom_3": "36",
      "Guide": "21",
      "Page_Up": "37",
      "Page_Down": "38",
      "Previous": "39",
      "Volume_Up": "40",
      "Volume_Down": "41",
      "Channel_Up": "42",
      "Channel_Down": "43",
      "Up": "22",
      "Down": "28",
      "Left": "24",
      "Right": "26",
      "Select": "25",
      "Mute": "44",
      "Info": "23",
      "Menu": "29",
      "Back": "27",
      "DVR": "45",
      "Rewind": "46",
      "Fast_Forward": "47",
      "Skip_Back": "14",
      "Play": "11",
      "Skip_Forward": "15",
      "Record": "48",
      "Pause": "12",
      "Stop": "13",
      "Red": "49",
      "Green": "50",
      "Yellow": "51",
      "Blue": "52",
      "Digit_1": "53",
      "Digit_2": "54",
      "Digit_3": "55",
      "Digit_4": "56",
      "Digit_5": "57",
      "Digit_6": "58",
      "Digit_7": "59",
      "Digit_8": "60",
      "Digit_9": "61",
      "Star": "62",
      "Digit_0": "63",
      "Pound": "64"
    },
    "sources": {
      "Query": "65",
      "Discover_Sources": "66",
      "Inspect_Room_State": "67",
      "Inspect_Now_Playing": "68",
      "Inspect_Media_Catalog": "69",
      "Browse_Media_Playlists": "70",
      "Browse_Media_Albums": "71",
      "Browse_Media_Audio_Stations": "72",
      "Browse_Media_Movies": "73",
      "Browse_Media_Video_Stations": "74",
      "Select_Media_ID_btn": "75",
      "Inspect_Service_Presets": "76",
      "Browse_Service_Presets": "77",
      "Video_Source_Select": "5",
      "Audio_Source_Select": "6",
      "Media_Item_Select": "7",
      "Video_Source_Name$": "9",
      "Audio_Source_Name$": "10",
      "Media_ID$": "11"
    },
    "transportSetup": {
      "Discover_Commands": "90"
    },
    "nowPlayingPage": "20"
  },
  "receive": {
    "audioSources": "3",
    "videoSources": "4",
    "mediaList": "5",
    "diagnostic": "6",
    "roomName": "7",
    "transport": "8",
    "connected": "7",
    "sourceValid": "8",
    "mediaValid": "9",
    "selectReady": "10",
    "muted": "16",
    "volumeValid": "17",
    "power": "18",
    "volume": "1",
    "nowPlaying": "16",
    "sources": {
      "Initialized_FB": "78",
      "Valid_FB": "79",
      "Power_Valid_FB": "80",
      "Mute_Valid_FB": "81",
      "Has_Video_Sources_FB": "82",
      "Has_Audio_Sources_FB": "83",
      "Content_Browse_Available_FB": "84",
      "Media_List_Partial_FB": "85",
      "Video_Source_Selected_Valid_FB": "86",
      "Audio_Source_Selected_Valid_FB": "87",
      "Video_Source_Overflow_FB": "88",
      "Audio_Source_Overflow_FB": "89",
      "Video_Source_Count_FB": "8",
      "Audio_Source_Count_FB": "9",
      "Audio_List_Page_FB": "3",
      "Video_List_Page_FB": "4",
      "Audio_List_Pages_FB": "10",
      "Video_List_Pages_FB": "11",
      "Video_Source_Selected_FB": "12",
      "Audio_Source_Selected_FB": "13",
      "Video_Source_Overflow_Count_FB": "14",
      "Audio_Source_Overflow_Count_FB": "15",
      "Media_Item_Count_FB": "16",
      "Media_List_Page_FB": "2",
      "Media_List_Pages_FB": "17",
      "Now_Playing_Page_FB": "20",
      "Now_Playing_Pages_FB": "21",
      "Media_Kind$": "12",
      "Source_Overflow_Message$": "13"
    },
    "transportStatus": {
      "Initialized_FB": "91",
      "Discovery_Valid_FB": "92",
      "Play_Available_FB": "93",
      "Pause_Available_FB": "94",
      "Stop_Available_FB": "95",
      "Watch_UI_Request": "96",
      "Listen_UI_Request": "97",
      "List_UI_Request": "98",
      "Advertised_Command_Count_FB": "18",
      "Mapped_Key_Count_FB": "19",
      "Local_Action$": "14",
      "Diagnostic$": "15"
    },
    "nowPlayingValid": "99",
    "progress": "17"
  },
  "extraReceive": [
    {
      "module": "Sources",
      "pin": "Initialized_FB",
      "type": "b",
      "join": "78"
    },
    {
      "module": "Sources",
      "pin": "Valid_FB",
      "type": "b",
      "join": "79"
    },
    {
      "module": "Sources",
      "pin": "Power_Valid_FB",
      "type": "b",
      "join": "80"
    },
    {
      "module": "Sources",
      "pin": "Mute_Valid_FB",
      "type": "b",
      "join": "81"
    },
    {
      "module": "Sources",
      "pin": "Has_Video_Sources_FB",
      "type": "b",
      "join": "82"
    },
    {
      "module": "Sources",
      "pin": "Has_Audio_Sources_FB",
      "type": "b",
      "join": "83"
    },
    {
      "module": "Sources",
      "pin": "Content_Browse_Available_FB",
      "type": "b",
      "join": "84"
    },
    {
      "module": "Sources",
      "pin": "Media_List_Partial_FB",
      "type": "b",
      "join": "85"
    },
    {
      "module": "Sources",
      "pin": "Video_Source_Selected_Valid_FB",
      "type": "b",
      "join": "86"
    },
    {
      "module": "Sources",
      "pin": "Audio_Source_Selected_Valid_FB",
      "type": "b",
      "join": "87"
    },
    {
      "module": "Sources",
      "pin": "Video_Source_Overflow_FB",
      "type": "b",
      "join": "88"
    },
    {
      "module": "Sources",
      "pin": "Audio_Source_Overflow_FB",
      "type": "b",
      "join": "89"
    },
    {
      "module": "Sources",
      "pin": "Video_Source_Count_FB",
      "type": "n",
      "join": "8"
    },
    {
      "module": "Sources",
      "pin": "Audio_Source_Count_FB",
      "type": "n",
      "join": "9"
    },
    {
      "module": "Sources",
      "pin": "Audio_List_Page_FB",
      "type": "n",
      "join": "3"
    },
    {
      "module": "Sources",
      "pin": "Video_List_Page_FB",
      "type": "n",
      "join": "4"
    },
    {
      "module": "Sources",
      "pin": "Audio_List_Pages_FB",
      "type": "n",
      "join": "10"
    },
    {
      "module": "Sources",
      "pin": "Video_List_Pages_FB",
      "type": "n",
      "join": "11"
    },
    {
      "module": "Sources",
      "pin": "Video_Source_Selected_FB",
      "type": "n",
      "join": "12"
    },
    {
      "module": "Sources",
      "pin": "Audio_Source_Selected_FB",
      "type": "n",
      "join": "13"
    },
    {
      "module": "Sources",
      "pin": "Video_Source_Overflow_Count_FB",
      "type": "n",
      "join": "14"
    },
    {
      "module": "Sources",
      "pin": "Audio_Source_Overflow_Count_FB",
      "type": "n",
      "join": "15"
    },
    {
      "module": "Sources",
      "pin": "Media_Item_Count_FB",
      "type": "n",
      "join": "16"
    },
    {
      "module": "Sources",
      "pin": "Media_List_Page_FB",
      "type": "n",
      "join": "2"
    },
    {
      "module": "Sources",
      "pin": "Media_List_Pages_FB",
      "type": "n",
      "join": "17"
    },
    {
      "module": "Sources",
      "pin": "Now_Playing_Page_FB",
      "type": "n",
      "join": "20"
    },
    {
      "module": "Sources",
      "pin": "Now_Playing_Pages_FB",
      "type": "n",
      "join": "21"
    },
    {
      "module": "Sources",
      "pin": "Media_Kind$",
      "type": "s",
      "join": "12"
    },
    {
      "module": "Sources",
      "pin": "Source_Overflow_Message$",
      "type": "s",
      "join": "13"
    },
    {
      "module": "Transport",
      "pin": "Initialized_FB",
      "type": "b",
      "join": "91"
    },
    {
      "module": "Transport",
      "pin": "Discovery_Valid_FB",
      "type": "b",
      "join": "92"
    },
    {
      "module": "Transport",
      "pin": "Play_Available_FB",
      "type": "b",
      "join": "93"
    },
    {
      "module": "Transport",
      "pin": "Pause_Available_FB",
      "type": "b",
      "join": "94"
    },
    {
      "module": "Transport",
      "pin": "Stop_Available_FB",
      "type": "b",
      "join": "95"
    },
    {
      "module": "Transport",
      "pin": "Watch_UI_Request",
      "type": "b",
      "join": "96"
    },
    {
      "module": "Transport",
      "pin": "Listen_UI_Request",
      "type": "b",
      "join": "97"
    },
    {
      "module": "Transport",
      "pin": "List_UI_Request",
      "type": "b",
      "join": "98"
    },
    {
      "module": "Transport",
      "pin": "Advertised_Command_Count_FB",
      "type": "n",
      "join": "18"
    },
    {
      "module": "Transport",
      "pin": "Mapped_Key_Count_FB",
      "type": "n",
      "join": "19"
    },
    {
      "module": "Transport",
      "pin": "Local_Action$",
      "type": "s",
      "join": "14"
    },
    {
      "module": "Transport",
      "pin": "Diagnostic$",
      "type": "s",
      "join": "15"
    }
  ],
  "bindings": [
    {
      "module": "Sources",
      "direction": "receive",
      "type": "s",
      "pin": "Now_Playing_Progress_JSON$",
      "key": "progress",
      "join": "17"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "n",
      "pin": "Now_Playing_Page_Set",
      "key": "nowPlayingPage",
      "join": "20"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "s",
      "pin": "Now_Playing_JSON$",
      "key": "nowPlaying",
      "join": "16"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Now_Playing_Valid_FB",
      "key": "nowPlayingValid",
      "join": "99"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "s",
      "pin": "Source_ID$",
      "key": "sourceId",
      "join": "1"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "s",
      "pin": "Media_Select_Request$",
      "key": "mediaRequest",
      "join": "2"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "n",
      "pin": "Volume_Set",
      "key": "volume",
      "join": "1"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "n",
      "pin": "Media_List_Page_Set",
      "key": "mediaPage",
      "join": "2"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "n",
      "pin": "Audio_List_Page_Set",
      "key": "audioPage",
      "join": "3"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "n",
      "pin": "Video_List_Page_Set",
      "key": "videoPage",
      "join": "4"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Select_Audio_ID_btn",
      "key": "selectAudio",
      "join": "1"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Select_Video_ID_btn",
      "key": "selectVideo",
      "join": "2"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Mute_On_btn",
      "key": "muteOn",
      "join": "3"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Mute_Off_btn",
      "key": "muteOff",
      "join": "4"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Room_Off_btn",
      "key": "off",
      "join": "5"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Pandora_Shuffle_On",
      "key": "shuffle",
      "join": "6"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "s",
      "pin": "Audio_Source_List_JSON$",
      "key": "audioSources",
      "join": "3"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "s",
      "pin": "Video_Source_List_JSON$",
      "key": "videoSources",
      "join": "4"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "s",
      "pin": "Media_List_JSON$",
      "key": "mediaList",
      "join": "5"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "s",
      "pin": "Diagnostic$",
      "key": "diagnostic",
      "join": "6"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "s",
      "pin": "Room_Name$",
      "key": "roomName",
      "join": "7"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Sources_Valid_FB",
      "key": "sourceValid",
      "join": "8"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Media_List_Valid_FB",
      "key": "mediaValid",
      "join": "9"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Media_Select_Available_FB",
      "key": "selectReady",
      "join": "10"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Mute_FB",
      "key": "muted",
      "join": "16"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Volume_Valid_FB",
      "key": "volumeValid",
      "join": "17"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Power_FB",
      "key": "power",
      "join": "18"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "n",
      "pin": "Volume_FB",
      "key": "volume",
      "join": "1"
    },
    {
      "module": "Gateway",
      "direction": "receive",
      "type": "b",
      "pin": "Connected_FB",
      "key": "connected",
      "join": "7"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Watch_btn",
      "key": "transport.Watch",
      "join": "30"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Control4_btn",
      "key": "transport.Control4",
      "join": "31"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Listen_btn",
      "key": "transport.Listen",
      "join": "32"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "List_btn",
      "key": "transport.List",
      "join": "33"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Custom_1_btn",
      "key": "transport.Custom_1",
      "join": "34"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Custom_2_btn",
      "key": "transport.Custom_2",
      "join": "35"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Custom_3_btn",
      "key": "transport.Custom_3",
      "join": "36"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Guide_btn",
      "key": "transport.Guide",
      "join": "21"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Page_Up_btn",
      "key": "transport.Page_Up",
      "join": "37"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Page_Down_btn",
      "key": "transport.Page_Down",
      "join": "38"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Previous_btn",
      "key": "transport.Previous",
      "join": "39"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Volume_Up_btn",
      "key": "transport.Volume_Up",
      "join": "40"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Volume_Down_btn",
      "key": "transport.Volume_Down",
      "join": "41"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Channel_Up_btn",
      "key": "transport.Channel_Up",
      "join": "42"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Channel_Down_btn",
      "key": "transport.Channel_Down",
      "join": "43"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Up_btn",
      "key": "transport.Up",
      "join": "22"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Down_btn",
      "key": "transport.Down",
      "join": "28"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Left_btn",
      "key": "transport.Left",
      "join": "24"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Right_btn",
      "key": "transport.Right",
      "join": "26"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Select_btn",
      "key": "transport.Select",
      "join": "25"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Mute_btn",
      "key": "transport.Mute",
      "join": "44"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Info_btn",
      "key": "transport.Info",
      "join": "23"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Menu_btn",
      "key": "transport.Menu",
      "join": "29"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Back_btn",
      "key": "transport.Back",
      "join": "27"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "DVR_btn",
      "key": "transport.DVR",
      "join": "45"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Rewind_btn",
      "key": "transport.Rewind",
      "join": "46"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Fast_Forward_btn",
      "key": "transport.Fast_Forward",
      "join": "47"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Skip_Back_btn",
      "key": "transport.Skip_Back",
      "join": "14"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Play_btn",
      "key": "transport.Play",
      "join": "11"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Skip_Forward_btn",
      "key": "transport.Skip_Forward",
      "join": "15"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Record_btn",
      "key": "transport.Record",
      "join": "48"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Pause_btn",
      "key": "transport.Pause",
      "join": "12"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Stop_btn",
      "key": "transport.Stop",
      "join": "13"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Red_btn",
      "key": "transport.Red",
      "join": "49"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Green_btn",
      "key": "transport.Green",
      "join": "50"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Yellow_btn",
      "key": "transport.Yellow",
      "join": "51"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Blue_btn",
      "key": "transport.Blue",
      "join": "52"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Digit_1_btn",
      "key": "transport.Digit_1",
      "join": "53"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Digit_2_btn",
      "key": "transport.Digit_2",
      "join": "54"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Digit_3_btn",
      "key": "transport.Digit_3",
      "join": "55"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Digit_4_btn",
      "key": "transport.Digit_4",
      "join": "56"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Digit_5_btn",
      "key": "transport.Digit_5",
      "join": "57"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Digit_6_btn",
      "key": "transport.Digit_6",
      "join": "58"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Digit_7_btn",
      "key": "transport.Digit_7",
      "join": "59"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Digit_8_btn",
      "key": "transport.Digit_8",
      "join": "60"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Digit_9_btn",
      "key": "transport.Digit_9",
      "join": "61"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Star_btn",
      "key": "transport.Star",
      "join": "62"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Digit_0_btn",
      "key": "transport.Digit_0",
      "join": "63"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Pound_btn",
      "key": "transport.Pound",
      "join": "64"
    },
    {
      "module": "Transport",
      "direction": "receive",
      "type": "s",
      "pin": "Key_Capabilities_JSON$",
      "key": "transport",
      "join": "8"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Query",
      "key": "sources.Query",
      "join": "65"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Discover_Sources",
      "key": "sources.Discover_Sources",
      "join": "66"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Inspect_Room_State",
      "key": "sources.Inspect_Room_State",
      "join": "67"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Inspect_Now_Playing",
      "key": "sources.Inspect_Now_Playing",
      "join": "68"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Inspect_Media_Catalog",
      "key": "sources.Inspect_Media_Catalog",
      "join": "69"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Browse_Media_Playlists",
      "key": "sources.Browse_Media_Playlists",
      "join": "70"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Browse_Media_Albums",
      "key": "sources.Browse_Media_Albums",
      "join": "71"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Browse_Media_Audio_Stations",
      "key": "sources.Browse_Media_Audio_Stations",
      "join": "72"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Browse_Media_Movies",
      "key": "sources.Browse_Media_Movies",
      "join": "73"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Browse_Media_Video_Stations",
      "key": "sources.Browse_Media_Video_Stations",
      "join": "74"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Select_Media_ID_btn",
      "key": "sources.Select_Media_ID_btn",
      "join": "75"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Inspect_Service_Presets",
      "key": "sources.Inspect_Service_Presets",
      "join": "76"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Browse_Service_Presets",
      "key": "sources.Browse_Service_Presets",
      "join": "77"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "n",
      "pin": "Video_Source_Select",
      "key": "sources.Video_Source_Select",
      "join": "5"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "n",
      "pin": "Audio_Source_Select",
      "key": "sources.Audio_Source_Select",
      "join": "6"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "n",
      "pin": "Media_Item_Select",
      "key": "sources.Media_Item_Select",
      "join": "7"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "s",
      "pin": "Video_Source_Name$",
      "key": "sources.Video_Source_Name$",
      "join": "9"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "s",
      "pin": "Audio_Source_Name$",
      "key": "sources.Audio_Source_Name$",
      "join": "10"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "s",
      "pin": "Media_ID$",
      "key": "sources.Media_ID$",
      "join": "11"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Initialized_FB",
      "key": "sources.Initialized_FB",
      "join": "78"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Valid_FB",
      "key": "sources.Valid_FB",
      "join": "79"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Power_Valid_FB",
      "key": "sources.Power_Valid_FB",
      "join": "80"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Mute_Valid_FB",
      "key": "sources.Mute_Valid_FB",
      "join": "81"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Has_Video_Sources_FB",
      "key": "sources.Has_Video_Sources_FB",
      "join": "82"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Has_Audio_Sources_FB",
      "key": "sources.Has_Audio_Sources_FB",
      "join": "83"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Content_Browse_Available_FB",
      "key": "sources.Content_Browse_Available_FB",
      "join": "84"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Media_List_Partial_FB",
      "key": "sources.Media_List_Partial_FB",
      "join": "85"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Video_Source_Selected_Valid_FB",
      "key": "sources.Video_Source_Selected_Valid_FB",
      "join": "86"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Audio_Source_Selected_Valid_FB",
      "key": "sources.Audio_Source_Selected_Valid_FB",
      "join": "87"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Video_Source_Overflow_FB",
      "key": "sources.Video_Source_Overflow_FB",
      "join": "88"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Audio_Source_Overflow_FB",
      "key": "sources.Audio_Source_Overflow_FB",
      "join": "89"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "n",
      "pin": "Video_Source_Count_FB",
      "key": "sources.Video_Source_Count_FB",
      "join": "8"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "n",
      "pin": "Audio_Source_Count_FB",
      "key": "sources.Audio_Source_Count_FB",
      "join": "9"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "n",
      "pin": "Audio_List_Page_FB",
      "key": "sources.Audio_List_Page_FB",
      "join": "3"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "n",
      "pin": "Video_List_Page_FB",
      "key": "sources.Video_List_Page_FB",
      "join": "4"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "n",
      "pin": "Audio_List_Pages_FB",
      "key": "sources.Audio_List_Pages_FB",
      "join": "10"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "n",
      "pin": "Video_List_Pages_FB",
      "key": "sources.Video_List_Pages_FB",
      "join": "11"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "n",
      "pin": "Video_Source_Selected_FB",
      "key": "sources.Video_Source_Selected_FB",
      "join": "12"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "n",
      "pin": "Audio_Source_Selected_FB",
      "key": "sources.Audio_Source_Selected_FB",
      "join": "13"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "n",
      "pin": "Video_Source_Overflow_Count_FB",
      "key": "sources.Video_Source_Overflow_Count_FB",
      "join": "14"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "n",
      "pin": "Audio_Source_Overflow_Count_FB",
      "key": "sources.Audio_Source_Overflow_Count_FB",
      "join": "15"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "n",
      "pin": "Media_Item_Count_FB",
      "key": "sources.Media_Item_Count_FB",
      "join": "16"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "n",
      "pin": "Media_List_Page_FB",
      "key": "sources.Media_List_Page_FB",
      "join": "2"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "n",
      "pin": "Media_List_Pages_FB",
      "key": "sources.Media_List_Pages_FB",
      "join": "17"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "n",
      "pin": "Now_Playing_Page_FB",
      "key": "sources.Now_Playing_Page_FB",
      "join": "20"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "n",
      "pin": "Now_Playing_Pages_FB",
      "key": "sources.Now_Playing_Pages_FB",
      "join": "21"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "s",
      "pin": "Media_Kind$",
      "key": "sources.Media_Kind$",
      "join": "12"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "s",
      "pin": "Source_Overflow_Message$",
      "key": "sources.Source_Overflow_Message$",
      "join": "13"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Discover_Commands",
      "key": "transportSetup.Discover_Commands",
      "join": "90"
    },
    {
      "module": "Transport",
      "direction": "receive",
      "type": "b",
      "pin": "Initialized_FB",
      "key": "transportStatus.Initialized_FB",
      "join": "91"
    },
    {
      "module": "Transport",
      "direction": "receive",
      "type": "b",
      "pin": "Discovery_Valid_FB",
      "key": "transportStatus.Discovery_Valid_FB",
      "join": "92"
    },
    {
      "module": "Transport",
      "direction": "receive",
      "type": "b",
      "pin": "Play_Available_FB",
      "key": "transportStatus.Play_Available_FB",
      "join": "93"
    },
    {
      "module": "Transport",
      "direction": "receive",
      "type": "b",
      "pin": "Pause_Available_FB",
      "key": "transportStatus.Pause_Available_FB",
      "join": "94"
    },
    {
      "module": "Transport",
      "direction": "receive",
      "type": "b",
      "pin": "Stop_Available_FB",
      "key": "transportStatus.Stop_Available_FB",
      "join": "95"
    },
    {
      "module": "Transport",
      "direction": "receive",
      "type": "b",
      "pin": "Watch_UI_Request",
      "key": "transportStatus.Watch_UI_Request",
      "join": "96"
    },
    {
      "module": "Transport",
      "direction": "receive",
      "type": "b",
      "pin": "Listen_UI_Request",
      "key": "transportStatus.Listen_UI_Request",
      "join": "97"
    },
    {
      "module": "Transport",
      "direction": "receive",
      "type": "b",
      "pin": "List_UI_Request",
      "key": "transportStatus.List_UI_Request",
      "join": "98"
    },
    {
      "module": "Transport",
      "direction": "receive",
      "type": "n",
      "pin": "Advertised_Command_Count_FB",
      "key": "transportStatus.Advertised_Command_Count_FB",
      "join": "18"
    },
    {
      "module": "Transport",
      "direction": "receive",
      "type": "n",
      "pin": "Mapped_Key_Count_FB",
      "key": "transportStatus.Mapped_Key_Count_FB",
      "join": "19"
    },
    {
      "module": "Transport",
      "direction": "receive",
      "type": "s",
      "pin": "Local_Action$",
      "key": "transportStatus.Local_Action$",
      "join": "14"
    },
    {
      "module": "Transport",
      "direction": "receive",
      "type": "s",
      "pin": "Diagnostic$",
      "key": "transportStatus.Diagnostic$",
      "join": "15"
    }
  ],
  "moduleOnly": [
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Select_Service_Preset_btn",
      "reason": "Private manual service-selector commissioning path; keep opaque selectors out of the UI"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Video_Source[VideoSources]",
      "reason": "Fixed source-slot array; dynamic JSON/ID UI replaces individual panel joins"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "b",
      "pin": "Audio_Source[AudioSources]",
      "reason": "Fixed source-slot array; dynamic JSON/ID UI replaces individual panel joins"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "n",
      "pin": "Service_Preset_Driver_ID",
      "reason": "Private manual service-selector commissioning path; keep opaque selectors out of the UI"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "n",
      "pin": "Service_Preset_Source_ID",
      "reason": "Private manual service-selector commissioning path; keep opaque selectors out of the UI"
    },
    {
      "module": "Sources",
      "direction": "send",
      "type": "s",
      "pin": "Service_Preset$[2048]",
      "reason": "Private manual service-selector commissioning path; keep opaque selectors out of the UI"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Video_Source_Available[VideoSources]",
      "reason": "Fixed source-slot array; dynamic JSON/ID UI replaces individual panel joins"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "b",
      "pin": "Audio_Source_Available[AudioSources]",
      "reason": "Fixed source-slot array; dynamic JSON/ID UI replaces individual panel joins"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "s",
      "pin": "Video_Source_Name_FB$[VideoSources]",
      "reason": "Fixed source-slot array; dynamic JSON/ID UI replaces individual panel joins"
    },
    {
      "module": "Sources",
      "direction": "receive",
      "type": "s",
      "pin": "Audio_Source_Name_FB$[AudioSources]",
      "reason": "Fixed source-slot array; dynamic JSON/ID UI replaces individual panel joins"
    },
    {
      "module": "Transport",
      "direction": "send",
      "type": "b",
      "pin": "Room_Off_btn",
      "reason": "Duplicate endpoint; UI Room Off is routed only to Sources.Room_Off_btn on digital5"
    }
  ]
};
