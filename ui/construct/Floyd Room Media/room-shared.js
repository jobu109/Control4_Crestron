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

;
// Keep this room list aligned with Room_IDs$ on the panel's router symbol.
window.ROOM_MEDIA_ROOMS=[
  {id:13,name:'Living Room',config:window.ROOM_MEDIA_CONFIG},
  {id:212,name:'Kitchen',config:window.ROOM_MEDIA_CONFIG}
];

;
(function(global){
  'use strict';
  function Collector(roomId,kind){this.roomId=roomId;this.kind=kind;this.reset();}
  Collector.prototype.reset=function(){this.revision=null;this.pages=new Map();this.total=null;this.count=null;this.pending=null;};
  Collector.prototype.accept=function(packet){
    if(!packet||packet.room_id!==this.roomId||packet.kind!==this.kind)return null;
    if(!packet.valid){this.reset();return {valid:false,pending:true,items:[],revision:'',source_id:null};}
    if(typeof packet.revision!=='string'||!packet.revision||!Number.isInteger(packet.page)||!Number.isInteger(packet.pages)||packet.page<1||packet.pages<1||packet.page>packet.pages||packet.pages>2048||!Number.isInteger(packet.total)||packet.total<0||packet.total>2048||!Array.isArray(packet.items))throw Error('Invalid media packet');
    if(packet.items.some(x=>typeof x.id!=='string'||typeof x.name!=='string'))throw Error('Invalid media item');
    if(this.revision!==packet.revision){this.reset();this.revision=packet.revision;this.total=packet.total;this.count=packet.pages;}
    if(packet.total!==this.total||packet.pages!==this.count)throw Error('Inconsistent media revision');
    this.pages.set(packet.page,packet.items);
    if(this.pending===packet.page)this.pending=null;
    let missing=0;for(let i=1;i<=this.count;i++)if(!this.pages.has(i)){missing=i;break;}
    if(missing){const request=this.pending===missing?null:missing;this.pending=missing;return {valid:false,pending:true,items:[],revision:this.revision,source_id:packet.source_id,requestPage:request};}
    const items=[];for(let i=1;i<=this.count;i++)items.push(...this.pages.get(i));
    if(items.length!==this.total||new Set(items.map(x=>x.id)).size!==items.length)throw Error('Incomplete or duplicate media list');
    return {valid:!!packet.selection_supported,pending:false,items,revision:this.revision,source_id:packet.source_id,driver_id:packet.driver_id,shuffle:packet.pandora_shuffle==='On'};
  };
  global.RoomMediaCatalog={Collector};
})(typeof window!=='undefined'?window:globalThis);

;
(function(global){
  'use strict';
  const keys=['Room_Off','Watch','Control4','Listen','List','Custom_1','Custom_2','Custom_3','Guide','Page_Up','Page_Down','Previous','Volume_Up','Volume_Down','Channel_Up','Channel_Down','Up','Down','Left','Right','Select','Mute','Info','Menu','Back','DVR','Rewind','Fast_Forward','Skip_Back','Play','Skip_Forward','Record','Pause','Stop','Red','Green','Yellow','Blue','Digit_1','Digit_2','Digit_3','Digit_4','Digit_5','Digit_6','Digit_7','Digit_8','Digit_9','Star','Digit_0','Pound'];
  function Collector(room,kind){this.room=room;this.kind=kind;this.retired=new Set();this.reset();}
  Collector.prototype.reset=function(){this.epoch=null;this.parts=new Map();this.count=0;this.pending=null;this.complete=false;};
  Collector.prototype.accept=function(frame){
    if(frame.r!==this.room||frame.k!==this.kind||this.retired.has(frame.e))return null;
    if(frame.w!==1||!Number.isInteger(frame.p)||!Number.isInteger(frame.n)||frame.p<1||frame.n<1||frame.p>frame.n||frame.n>2200||typeof frame.e!=='string'||!/^[a-f0-9]{16}$/.test(frame.e)||typeof frame.d!=='string'||frame.d.length>160||!/^[A-Za-z0-9+/]*={0,2}$/.test(frame.d))throw Error('Invalid serial frame');
    let changed=false;
    if(frame.e!==this.epoch){if(this.epoch)this.retired.add(this.epoch);if(this.retired.size>32)this.retired.delete(this.retired.values().next().value);this.reset();this.epoch=frame.e;this.count=frame.n;changed=true;}
    if(this.count!==frame.n)throw Error('Inconsistent serial frame count');
    if(this.complete)return null;
    if(this.parts.has(frame.p)&&this.parts.get(frame.p)!==frame.d)throw Error('Conflicting serial frame');
    this.parts.set(frame.p,frame.d);if(this.pending===frame.p)this.pending=null;
    let missing=0;for(let i=1;i<=this.count;i++)if(!this.parts.has(i)){missing=i;break;}
    if(missing){const requestPage=this.pending===missing?null:missing;this.pending=missing;return {changed,requestPage};}
    let text='';for(let i=1;i<=this.count;i++)text+=this.parts.get(i);
    const bytes=Uint8Array.from(global.atob(text),c=>c.charCodeAt(0));if(bytes.length>262144)throw Error('Serial document too large');
    const packet=JSON.parse(new global.TextDecoder('utf-8',{fatal:true}).decode(bytes));
    if(packet.room_id!==this.room||packet.wire_error)throw Error('Invalid serial document');
    this.complete=true;return {changed,packet};
  };
  function capabilities(packet){
    if(packet.wire!=='keys1')return packet;
    if(typeof packet.states!=='string'||packet.states.length!==keys.length||!/^[alu]+$/.test(packet.states))throw Error('Invalid key capabilities');
    return {room_id:packet.room_id,keys:keys.map((key,index)=>({key,state:{a:'advertised',l:'local_ui',u:'unavailable'}[packet.states[index]]}))};
  }
  global.RoomSerialWire={Collector,capabilities,keys};
})(window);

;
(function(global){
  'use strict';
  function Bridge(com,config){
    this.destroyed=false;this.started=false;this.com=com;this.config=config;this.subs=[];this.listener=null;this.connected=false;this.gatewayConnected=false;this.transportConnected=!config.browser;this.ready=false;this.catalogReady=false;this.sequence=0;this.timers=[];this.activePulses=new Set();this.capabilities={};this.feedback={};
    this.progress=null;this.nowPlaying=null;this.playingValid=false;this.wire={};for(const kind of ['a','v','m','n'])if(global.RoomSerialWire)this.wire[kind]=new global.RoomSerialWire.Collector(config.roomId,kind);this.pageRequests=new Map();
    this.collector=new RoomMediaCatalog.Collector(config.roomId,'service_presets');this.sources={listen:new Map(),watch:new Map()};
  }
  Bridge.prototype.emit=function(update){if(this.listener)this.listener(update);};
  Bridge.prototype.watch=function(type,join,fn){if(join)this.subs.push([type,String(join),this.com.subscribeState(type,String(join),fn)]);};
  Bridge.prototype.subscribe=function(listener){
    if(this.destroyed)throw Error('Bridge already destroyed');if(this.started)throw Error('Bridge already subscribed');this.started=true;
    this.listener=listener;const r=this.config.receive;
    this.watch('b',r.connected,value=>{this.gatewayConnected=!!value;this.refreshConnection();if(!value){this.catalogReady=false;this.collector.reset();this.emit({catalog:{items:[],valid:false,pending:false,revision:'',source_id:null}});}});
    this.watch('b',r.selectReady,value=>{this.ready=!!value;this.emit({selectionReady:this.ready});});
    this.watch('b',r.mediaValid,value=>{if(!value){this.catalogReady=false;this.collector.reset();this.emit({catalog:{items:[],valid:false,pending:this.connected,revision:'',source_id:null}});}});
    this.watch('n',r.volume,value=>this.emit({volume:Number(value)}));this.watch('b',r.muted,value=>this.emit({muted:!!value}));
    this.watch('s',r.roomName,value=>this.emit({roomName:String(value)}));
    this.watch('b',r.volumeValid,value=>this.emit({volumeValid:!!value}));
    this.watch('b',r.sourceValid,value=>this.emit({sourceValid:!!value}));
    this.watch('b',r.power,value=>this.emit({power:!!value}));
    for(const signal of this.config.extraReceive||[])this.watch(signal.type,signal.join,value=>{
      this.feedback[signal.module+'.'+signal.pin]=value;
      this.emit({moduleFeedback:{...this.feedback}});this.publishSelectedSources();
    });
    this.watch('s',r.transport,value=>{try{let p=JSON.parse(value);if(global.RoomSerialWire)p=global.RoomSerialWire.capabilities(p);if(p.room_id!==this.config.roomId)return;const capabilities={};for(const item of p.keys||[])capabilities[item.key]=item.state==='advertised'||item.state==='local_ui';this.capabilities=capabilities;this.emit({capabilities});}catch(_){/* Empty initial state is normal. */}});
    for(const mode of ['listen','watch'])this.watch('s',mode==='listen'?r.audioSources:r.videoSources,value=>{
      try{if(!value)return;const p=this.decode(value,mode==='listen'?'a':'v',mode==='listen'?this.config.send.audioPage:this.config.send.videoPage);if(!p)return;if(p.room_id!==this.config.roomId||p.mode!==mode)return;const cache=this.sources[mode];if(p.page===1)cache.clear();if(!p.valid){cache.clear();this.emit(mode==='listen'?{sources:[]}:{videoSources:[]});return;}if(!Number.isInteger(p.page)||!Number.isInteger(p.pages)||!Array.isArray(p.items))return;cache.set(p.page,p.items);let missing=0;for(let i=1;i<=p.pages;i++)if(!cache.has(i)){missing=i;break;}if(missing){this.requestPage(mode==='listen'?this.config.send.audioPage:this.config.send.videoPage,missing);return;}const items=[];for(let i=1;i<=p.pages;i++)items.push(...cache.get(i));if(items.length===p.total){this.emit(mode==='listen'?{sources:items}:{videoSources:items});this.publishSelectedSources();}}catch(_){this.sources[mode].clear();this.emit(mode==='listen'?{sources:[]}:{videoSources:[]});this.publishSelectedSources();this.emit({status:'Source list unavailable'});}
    });
    this.watch('s',r.mediaList,value=>{
      if(!value)return;
      try{const packet=this.decode(value,'m',this.config.send.mediaPage);if(!packet)return;const result=this.collector.accept(packet);if(!result)return;this.catalogReady=result.valid;this.emit({catalog:result});if(result.requestPage)this.requestPage(this.config.send.mediaPage,result.requestPage);}
      catch(_){this.catalogReady=false;this.collector.reset();this.emit({catalog:{items:[],valid:false,pending:false,revision:'',source_id:null},status:'Station list unavailable'});}
    });
    this.watch('s',r.progress,value=>{
      try{const p=JSON.parse(value);if(p.room_id!==this.config.roomId)return;
        if(typeof p.valid!=='boolean')throw Error('Invalid progress');
        if(p.valid&&(!Number.isInteger(p.source_id)||p.source_id<=0||typeof p.track_revision!=='string'||!/^[a-f0-9]{16}$/.test(p.track_revision)||!Number.isInteger(p.position_seconds)||!Number.isInteger(p.duration_seconds)||p.position_seconds<0||p.duration_seconds<=0||p.duration_seconds>172800||p.position_seconds>p.duration_seconds))throw Error('Invalid progress range');
        this.progress=p.valid?p:null;this.emit({progress:this.connected?this.progress:null});
      }catch(_){this.progress=null;this.emit({progress:null});}
    });
    this.watch('s',r.nowPlaying,value=>{
      if(!value)return;
      try{const packet=this.decode(value,'n',this.config.send.nowPlayingPage);if(!packet)return;
        if(packet.room_id!==this.config.roomId||typeof packet.valid!=='boolean')throw Error('Invalid metadata document');
        this.nowPlaying=packet.valid?packet:null;this.emit({nowPlaying:this.connected&&this.playingValid?this.nowPlaying:null});
      }catch(_){this.nowPlaying=null;this.emit({nowPlaying:null});}
    });
    this.watch('b',r.nowPlayingValid,value=>{
      this.playingValid=!!value;
      if(!value){this.nowPlaying=null;const wire=this.wire.n;if(wire&&wire.epoch){wire.retired.add(wire.epoch);wire.reset();}}
      this.emit({nowPlaying:this.connected&&this.playingValid?this.nowPlaying:null});
    });
    this.publishSelectedSources();
  };
  Bridge.prototype.decode=function(value,kind,join){
    const frame=JSON.parse(value);if(frame.w!==1)return frame;
    if(!this.wire[kind])throw Error('Serial frame decoder not loaded');
    const result=this.wire[kind].accept(frame);if(!result)return null;
    if(result.changed){
      if(kind==='m'){this.catalogReady=false;this.collector.reset();this.emit({catalog:{items:[],valid:false,pending:true,revision:'',source_id:null}});}
      else if(kind==='n'){this.nowPlaying=null;this.emit({nowPlaying:null});}
      else {const mode=kind==='a'?'listen':'watch';this.sources[mode].clear();this.emit(mode==='listen'?{sources:[]}:{videoSources:[]});this.publishSelectedSources();}
    }
    if(result.requestPage)this.requestPage(join,result.requestPage);
    return result.packet||null;
  };
  Bridge.prototype.requestPage=function(join,page){
    if(!this.connected||!join)return;
    const previous=this.pageRequests.get(join);if(previous&&previous.timer)clearTimeout(previous.timer);
    const request={page,timer:null};this.pageRequests.set(join,request);
    const publish=()=>{if(this.pageRequests.get(join)===request&&!this.destroyed)this.send('n',join,page);};
    if(!previous||previous.page===page){this.send('n',join,0);request.timer=setTimeout(publish,25);}
    else publish();
  };
  Bridge.prototype.publishSelectedSources=function(){
    const selectedSources={};
    for(const [mode,prefix]of [['listen','Audio'],['watch','Video']]){
      const index=Number(this.feedback['Sources.'+prefix+'_Source_Selected_FB']||0);
      const valid=!!this.feedback['Sources.'+prefix+'_Source_Selected_Valid_FB'];
      const rows=[];for(const key of Array.from(this.sources[mode].keys()).sort((a,b)=>a-b))rows.push(...this.sources[mode].get(key));
      selectedSources[mode]={index,valid:this.connected&&valid&&index>0&&index<=rows.length,id:valid&&index>0&&index<=rows.length?rows[index-1].id:null};
    }
    this.emit({selectedSources});
  };
  Bridge.prototype.refreshConnection=function(){
    const wasConnected=this.connected;
    this.connected=this.gatewayConnected&&this.transportConnected;
    this.emit({status:this.connected?'Control4 connected':!this.transportConnected?'Crestron connection unavailable':'Control4 disconnected',connected:this.connected});
    this.publishSelectedSources();
    if(wasConnected&&!this.connected){this.progress=null;this.nowPlaying=null;if(this.wire.n)this.wire.n.reset();}
    this.emit({nowPlaying:this.connected&&this.playingValid?this.nowPlaying:null});
    this.emit({progress:this.connected?this.progress:null});
    if(this.connected&&!wasConnected){
      // Serial state replay can arrive before Connected_FB. Resume its pending
      // packet request instead of waiting for the same serial value to change.
      const joins={a:this.config.send.audioPage,v:this.config.send.videoPage,m:this.config.send.mediaPage,n:this.config.send.nowPlayingPage};
      for(const [kind,collector]of Object.entries(this.wire))if(!collector.complete&&collector.pending)this.requestPage(joins[kind],collector.pending);
    }
  };
  Bridge.prototype.setTransportConnected=function(value){this.transportConnected=!!value;this.refreshConnection();};
  Bridge.prototype.send=function(type,join,value){if(this.destroyed||!this.connected||!join)return false;this.com.publishEvent(type,String(join),value);return true;};
  Bridge.prototype.pulse=function(join){if(this.send('b',join,true)){this.activePulses.add(String(join));this.timers.push(setTimeout(()=>{this.com.publishEvent('b',String(join),false);this.activePulses.delete(String(join));},100));}};
  Bridge.prototype.selectSource=function(mode,id){if(this.send('s',this.config.send.sourceId,String(id)))this.pulse(mode==='watch'?this.config.send.selectVideo:this.config.send.selectAudio);};
  Bridge.prototype.selectItem=function(kind,revision,id){
    if(!this.catalogReady||!this.ready||this.collector.revision!==revision)return;
    const items=[];for(const rows of this.collector.pages.values())items.push(...rows);if(!items.some(x=>x.id===String(id)))return;
    this.send('s',this.config.send.mediaRequest,JSON.stringify({room_id:this.config.roomId,kind,revision,id:String(id),request_id:Date.now()+'-'+(++this.sequence)}));
  };
  Bridge.prototype.transport=function(key){if(!this.capabilities[key])return;if(key==='Room_Off'){this.off();return;}this.pulse(this.config.send.transport[key]);};
  Bridge.prototype.volume=function(value){this.send('n',this.config.send.volume,Math.max(0,Math.min(100,Math.round(value))));};
  Bridge.prototype.mute=function(value){this.pulse(value?this.config.send.muteOn:this.config.send.muteOff);};
  Bridge.prototype.off=function(){this.pulse(this.config.send.off);};
  Bridge.prototype.shuffle=function(value){this.send('b',this.config.send.shuffle,!!value);};
  Bridge.prototype.destroy=function(){if(this.destroyed)return;this.destroyed=true;for(const request of this.pageRequests.values())if(request.timer)clearTimeout(request.timer);this.pageRequests.clear();for(const [type,join,id]of this.subs)this.com.unsubscribeState(type,join,id);for(const t of this.timers)clearTimeout(t);for(const join of this.activePulses)this.com.publishEvent('b',join,false);this.activePulses.clear();this.subs=[];this.listener=null;};
  global.CrestronRoomBridge=Bridge;
})(window);

;
(function(global){
  'use strict';
  function mount(root, options){
    if(!root || root.dataset.mounted) return;
    root.dataset.mounted='true';
    const data=options.data||{}, bridge=options.bridge||null;
    const state={mode:options.initialMode==='watch'?'watch':'listen',source:'1648',items:[],page:1,search:'',selected:'380',shuffle:false,volume:29,mute:false,valid:!bridge,revision:'preview',pending:false};
    const sample=data.metadata||{};
    root.className='rm';
    root.innerHTML=`<header class="rm-header"><div><div class="rm-brand">HOME / ENTERTAINMENT</div><select class="rm-room" aria-label="Room"><option>Living Room</option></select></div><nav class="rm-tabs" aria-label="Media mode"><button data-mode="listen" class="active">Listen</button><button data-mode="watch">Watch</button></nav><div class="rm-status">${bridge?'Connecting to Crestron':'Interactive preview'}</div></header><main class="rm-body"><aside class="rm-sources"><div class="rm-label">Sources</div><div class="rm-source-buttons"></div><div class="rm-sources-note">Your room.<br>Your music.</div></aside><section class="rm-browser"><div class="rm-list-head"><h1 class="rm-service-title">Pandora</h1><span class="rm-count"></span></div><div class="rm-subtitle">Saved stations</div><input class="rm-search" type="search" placeholder="Search saved stations" aria-label="Search stations"><div class="rm-list" role="list" aria-label="Saved stations"></div><div class="rm-page"><button data-page="previous" aria-label="Previous page">‹</button><span class="rm-page-text"></span><button data-page="next" aria-label="Next page">›</button></div></section><section class="rm-now"><div class="rm-label">Now playing</div><div class="rm-art"><small>ARTWORK PLACEHOLDER</small><strong>Sound<br>at home.</strong><span>Image feed to be connected</span></div><h2 class="rm-track"></h2><div class="rm-artist"></div><div class="rm-album"></div><div class="rm-playing-from"></div><div class="rm-progress" hidden><span class="rm-elapsed"></span><progress aria-label="Track progress"></progress><span class="rm-duration"></span></div><div class="rm-transport"><button data-key="Skip_Back" aria-label="Previous track">|‹</button><button data-key="Play" class="play" aria-label="Play">▶</button><button data-key="Pause" aria-label="Pause">Ⅱ</button><button data-key="Skip_Forward" aria-label="Next track">›|</button></div><label class="rm-shuffle"><input type="checkbox"> Shuffle on next station selection</label><div class="rm-meta-note"></div></section></main><footer class="rm-footer"><div class="rm-volume"><span>Volume</span><button class="rm-volume-down" data-key="Volume_Down" aria-label="Volume down">−</button><input type="range" min="0" max="100" value="29" aria-label="Room volume"><button class="rm-volume-up" data-key="Volume_Up" aria-label="Volume up">+</button><span class="rm-volume-value">29%</span></div><button class="rm-mute">Mute</button><button class="off">⏻ &nbsp; Room off</button></footer><div class="rm-preview">${bridge?'Live controls and metadata from Control4':'Preview only · station names come from your exports · controls do not operate the system'}</div><div class="rm-toast" role="status" hidden></div>`;
    const q=s=>root.querySelector(s);
    let pressedKey='',pressedTimer;
    function buttonKey(button){
      if(button.classList.contains('play'))return 'main-playback';
      for(const name of ['data-source','data-item','data-key','data-remote-key','data-mode','data-page','aria-label'])if(button.hasAttribute(name))return name+':'+button.getAttribute(name);
      return Array.from(button.classList).filter(name=>name!=='active'&&name!=='rm-pressed').join(' ')||button.textContent;
    }
    function paintPressed(){root.querySelectorAll('button').forEach(button=>button.classList.toggle('rm-pressed',!button.closest('.rm-devices')&&!!pressedKey&&!button.disabled&&buttonKey(button)===pressedKey));}
    function onPress(event){
      const button=event.target.closest('button');if(!button||!root.contains(button)||button.disabled)return;
      if(button.closest('.rm-devices'))return; pressedKey=buttonKey(button);clearTimeout(pressedTimer);paintPressed();queueMicrotask(paintPressed);
      pressedTimer=setTimeout(()=>{pressedKey='';paintPressed();},200);
    }
    root.addEventListener('click',onPress,true);
    const art=q('.rm-art'),artImage=document.createElement('img');
    artImage.className='rm-art-image';artImage.alt='Album artwork';artImage.referrerPolicy='no-referrer';art.prepend(artImage);
    artImage.onload=()=>art.classList.add('rm-art-loaded');artImage.onerror=()=>art.classList.remove('rm-art-loaded');
    let artworkUrl='';
    let toastTimer, metadata=bridge?null:sample, progress=null;
    let videoSources=bridge?[]:[{id:'2061',name:'DIRECTV Stream'}];
    let connected=!bridge,ready=!bridge,volumeValid=!bridge,muteValid=!bridge,capabilities={},selectedSources={};
    let sources=bridge?[]:[{id:'65',name:'Spotify'},{id:'1648',name:'Pandora'}];
    const updateDevices=bridge?.sendDevice&&global.PanelRoomDevices
      ?global.PanelRoomDevices.mount(q('.rm-now'),bridge.sendDevice):null;
    let deviceFeedback=[];
    const roomSelect=q('.rm-room');
    const rooms=options.rooms||[{id:13,name:'Living Room'}];
    roomSelect.replaceChildren();
    rooms.forEach(room=>{const option=document.createElement('option');option.value=String(room.id);option.textContent=room.name;roomSelect.append(option);});
    roomSelect.value=String(options.roomId||rooms[0].id);
    roomSelect.onchange=()=>{if(options.onRoomChange)options.onRoomChange(Number(roomSelect.value));};
    const pageSize=6;
    function toast(text){q('.rm-toast').textContent=text;q('.rm-toast').hidden=false;clearTimeout(toastTimer);toastTimer=setTimeout(()=>q('.rm-toast').hidden=true,2400);}
    function selectedName(){const item=state.items.find(i=>i.id===state.selected);return item?item.name:'';}
    function renderProgress(){
      const valid=!!(metadata&&progress&&progress.valid&&metadata.source_id===progress.source_id&&metadata.track_revision===progress.track_revision);
      q('.rm-progress').hidden=!valid;if(!valid)return;
      const time=value=>Math.floor(value/60)+':'+String(value%60).padStart(2,'0');
      q('.rm-elapsed').textContent=time(progress.position_seconds);q('.rm-duration').textContent=time(progress.duration_seconds);
      q('.rm-progress progress').max=progress.duration_seconds;q('.rm-progress progress').value=progress.position_seconds;
    }
    function render(){
      q('.rm-source-buttons').replaceChildren();
      const available=state.mode==='watch'?videoSources:sources;
      available.forEach(source=>{const b=document.createElement('button');b.className='rm-source'+((bridge?(selectedSources[state.mode]?.valid&&selectedSources[state.mode].id===source.id):source.id===state.source)?' active':'');b.dataset.source=source.id;b.setAttribute('aria-pressed',String(bridge?!!(selectedSources[state.mode]?.valid&&selectedSources[state.mode].id===source.id):source.id===state.source));const icon=document.createElement('span');icon.className='rm-icon';icon.textContent=source.name.slice(0,1);const name=document.createElement('span');name.textContent=source.name;b.append(icon,name);b.disabled=!connected;b.onclick=()=>selectSource(source.id);q('.rm-source-buttons').append(b);});
      const source=sources.find(s=>s.id===state.source);
      q('.rm-service-title').textContent=state.mode==='watch'?'Video controls':source?source.name:'Choose a source';
      q('.rm-subtitle').textContent=state.mode==='watch'?'Navigation and transport':state.source==='65'?'Saved presets':'Saved stations';
      q('.rm-search').hidden=state.mode==='watch';
      const filtered=state.items.filter(i=>i.name.toLowerCase().includes(state.search.toLowerCase()));
      const pages=Math.max(1,Math.ceil(filtered.length/pageSize));state.page=Math.min(state.page,pages);
      q('.rm-count').textContent=state.pending?'Select a service':state.mode==='watch'?'Remote':state.items.length+' '+(state.source==='65'?'presets':'stations');
      q('.rm-list').replaceChildren();
      if(state.mode==='watch'){
        const remote=document.createElement('div');remote.className='rm-watch';
        const groups=[
          ['Navigation',['Guide','Up','Info','Left','Select','Right','Back','Down','Menu']],
          ['Playback',['Rewind','Play','Fast_Forward','Skip_Back','Pause','Skip_Forward','Record','Stop','DVR']],
          ['Room',['Room_Off','Watch','Control4','Listen','List','Previous']],
          ['Volume and channel',['Volume_Up','Volume_Down','Mute','Channel_Up','Channel_Down','Page_Up','Page_Down']],
          ['Keypad',['Digit_1','Digit_2','Digit_3','Digit_4','Digit_5','Digit_6','Digit_7','Digit_8','Digit_9','Star','Digit_0','Pound']],
          ['Functions',['Red','Green','Yellow','Blue','Custom_1','Custom_2','Custom_3']]
        ];
        groups.forEach(([title,keys])=>{
          const heading=document.createElement('h3');heading.textContent=title;remote.append(heading);
          const grid=document.createElement('div');grid.className='rm-key-grid';
          keys.forEach(key=>{const b=document.createElement('button');b.dataset.remoteKey=key;b.textContent=key.startsWith('Digit_')?key.slice(6):key==='Star'?'*':key==='Pound'?'#':key.replaceAll('_',' ');b.disabled=!!bridge&&(!connected||!capabilities[key]);b.onclick=()=>command(key);grid.append(b);});remote.append(grid);
        });q('.rm-list').append(remote);
      }else if(state.pending || !filtered.length){const p=document.createElement('div');p.className='rm-empty';p.textContent=state.pending?'Please select a service from the left':state.valid?'No matching saved items':'Please select a service from the left';q('.rm-list').append(p);
      }else filtered.slice((state.page-1)*pageSize,state.page*pageSize).forEach(item=>{
        const b=document.createElement('button');b.className='rm-station'+(state.selected===item.id?' active':'');b.dataset.item=item.id;b.setAttribute('role','listitem');b.disabled=!state.valid||!connected||!ready;
        const icon=document.createElement('span');icon.className='rm-row-icon';icon.textContent='♫';const name=document.createElement('span');name.className='rm-station-name';name.textContent=item.name;const arrow=document.createElement('span');arrow.className='rm-row-arrow';arrow.textContent=state.selected===item.id?'●':'›';b.append(icon,name,arrow);b.onclick=()=>selectItem(item);q('.rm-list').append(b);
      });
      q('.rm-page-text').textContent=state.mode==='watch'?'Control4 remote':`Page ${state.page} of ${pages}${state.search?' · '+filtered.length+' matches':''}`;
      q('[data-page="previous"]').disabled=state.page<=1||state.mode==='watch';q('[data-page="next"]').disabled=state.page>=pages||state.mode==='watch';
      q('.rm-now').classList.toggle('rm-waiting',!metadata);
      q('.rm-track').textContent=metadata&&metadata.title||'Waiting for selection';q('.rm-artist').textContent=metadata&&metadata.artist||'—';q('.rm-album').textContent=metadata&&metadata.album||'';
      q('.rm-playing-from').textContent=metadata&&metadata.station?'Playing from · '+metadata.station:selectedName()?'Selected in preview · '+selectedName():'';
      q('.rm-meta-note').textContent=bridge?(metadata?'Live from Control4':'Waiting for Control4 metadata'):(metadata?'Text sample from the metadata capture; not a live feed':'Preview metadata');
      let nextArt='';try{const url=new URL(metadata&&metadata.artwork||'');if(url.protocol==='https:'&&!url.username&&!url.password&&!url.search&&!url.hash)nextArt=url.href;}catch(_){}
      if(nextArt!==artworkUrl){artworkUrl=nextArt;art.classList.remove('rm-art-loaded');if(nextArt)artImage.src=nextArt;else artImage.removeAttribute('src');}
      const action=metadata&&metadata.playback_action;
      const knownAction=action==='play'||action==='pause';
      const mainPlayback=q('.rm-transport .play'),secondaryPause=q('.rm-transport button[aria-label="Pause"]:not(.play)');
      mainPlayback.dataset.key=knownAction&&action==='pause'?'Pause':'Play';mainPlayback.textContent=mainPlayback.dataset.key==='Pause'?'Ⅱ':'▶';mainPlayback.setAttribute('aria-label',mainPlayback.dataset.key);
      secondaryPause.hidden=knownAction;
      root.querySelectorAll('[data-key]').forEach(b=>b.disabled=!!bridge&&(!connected||!capabilities[b.dataset.key]));
      q('.rm-volume input').disabled=!connected||!volumeValid;q('.rm-mute').disabled=!connected||!muteValid;q('.off').disabled=!connected;
      q('.rm-shuffle').hidden=state.source!=='1648'||state.mode==='watch';q('.rm-shuffle input').checked=state.shuffle;q('.rm-shuffle input').disabled=!connected;
      q('.rm-volume input').value=state.volume;q('.rm-volume-value').textContent=volumeValid?state.volume+'%':'—';q('.rm-mute').textContent=state.mute?'Unmute':'Mute';
      renderProgress();paintPressed();
    }
    function selectSource(id){
      if(bridge){state.pending=true;state.items=[];state.valid=false;state.selected='';render();bridge.selectSource(state.mode,id);return;}
      state.source=id;state.items=state.mode==='watch'?[]:(data.catalogs&&data.catalogs[id]||[]);state.selected='';state.page=1;state.search='';q('.rm-search').value='';metadata=null;state.valid=true;render();
    }
    function selectItem(item){
      if(bridge){bridge.selectItem('service_presets',state.revision,item.id);toast('Selection sent. Waiting for playback feedback.');return;}
      state.selected=item.id;metadata=null;render();toast('Preview selection · '+item.name);
    }
    function command(key){
      if(bridge)bridge.transport(key);else if(key==='Volume_Up'||key==='Volume_Down'){state.volume=Math.max(0,Math.min(100,state.volume+(key==='Volume_Up'?1:-1)));render();}else toast('Preview command · '+key);
      if(['Watch','Listen','List'].includes(key)){
        state.mode=key==='Watch'?'watch':'listen';state.page=1;
        root.querySelectorAll('[data-mode]').forEach(b=>b.classList.toggle('active',b.dataset.mode===state.mode));render();
        if(key==='List')q('.rm-search').focus();
      }
    }
    q('.rm-search').addEventListener('input',e=>{state.search=e.target.value;state.page=1;render();});
    root.querySelectorAll('[data-mode]').forEach(b=>b.onclick=()=>{state.mode=b.dataset.mode;state.page=1;root.querySelectorAll('[data-mode]').forEach(x=>x.classList.toggle('active',x===b));render();if(!bridge&&state.mode==='listen')selectSource('1648');});
    q('[data-page="previous"]').onclick=()=>{state.page--;render();};q('[data-page="next"]').onclick=()=>{state.page++;render();};
    root.querySelectorAll('[data-key]').forEach(b=>b.onclick=()=>command(b.dataset.key));
    q('.rm-shuffle input').onchange=e=>{state.shuffle=e.target.checked;if(bridge)bridge.shuffle(state.shuffle);};
    q('.rm-volume input').oninput=e=>q('.rm-volume-value').textContent=e.target.value+'%';
    q('.rm-volume input').onchange=e=>{if(bridge)bridge.volume(Number(e.target.value));else state.volume=Number(e.target.value);};
    q('.rm-mute').onclick=()=>{if(bridge)bridge.mute(!state.mute);else{state.mute=!state.mute;render();}};
    q('.off').onclick=()=>{if(bridge)bridge.off();else{state.items=[];state.selected='';state.source='';state.valid=false;metadata=null;render();toast('Preview · Room off');}};
    state.items=data.catalogs&&data.catalogs['1648']||[];
    if(bridge)bridge.subscribe(update=>{
      if(update.devices!==undefined)deviceFeedback=update.devices;
      if(update.progress!==undefined){progress=update.progress;if(Object.keys(update).length===1){renderProgress();return;}}
      if(update.selectedSources)selectedSources=update.selectedSources;
      if(update.volumeValid!==undefined)volumeValid=update.volumeValid;
      if(update.moduleFeedback&&update.moduleFeedback['Sources.Mute_Valid_FB']!==undefined)muteValid=!!update.moduleFeedback['Sources.Mute_Valid_FB'];
      if(update.sources)sources=update.sources;if(update.videoSources)videoSources=update.videoSources;if(update.connected!==undefined)connected=update.connected;if(update.selectionReady!==undefined)ready=update.selectionReady;if(update.capabilities)capabilities=update.capabilities;
      if(update.catalog){state.items=update.catalog.items;state.revision=update.catalog.revision;state.valid=update.catalog.valid;state.pending=update.catalog.pending;state.source=update.catalog.source_id?String(update.catalog.source_id):'';state.shuffle=update.catalog.shuffle||false;state.page=1;}
      if(update.volume!==undefined)state.volume=update.volume;if(update.muted!==undefined)state.mute=update.muted;
      if(update.roomName){const selected=Array.from(roomSelect.options).find(o=>o.value===roomSelect.value);if(selected)selected.textContent=update.roomName;}
      if(update.status)q('.rm-status').textContent=update.status;
      if(update.nowPlaying!==undefined)metadata=update.nowPlaying;
      if(updateDevices)updateDevices(connected,deviceFeedback);
      render();
    });
    root.querySelectorAll('[data-mode]').forEach(b=>b.classList.toggle('active',b.dataset.mode===state.mode));
    render();
    return {destroy(){clearTimeout(toastTimer);clearTimeout(pressedTimer);root.removeEventListener('click',onPress,true);if(bridge)bridge.destroy();root.replaceChildren();delete root.dataset.mounted;}};
  }
  global.RoomMediaApp={mount};
})(window);

;
window.ROOM_MEDIA_PREVIEW = {"catalogs":{"65":[{"id":"172","name":"90's Music","label_available":true},{"id":"168","name":"Classic Rock Favs","label_available":true},{"id":"170","name":"Emma's Favs","label_available":true},{"id":"169","name":"FAVS","label_available":true},{"id":"171","name":"HEAVY","label_available":true}],"1648":[{"id":"457","name":"'Weird Al' Yankovic Radio","label_available":true},{"id":"387","name":"1738 (feat. Coi Leray) Radio","label_available":true},{"id":"392","name":"1812 Overture, Finale Radio","label_available":true},{"id":"455","name":"90s Alternative  Radio","label_available":true},{"id":"395","name":"A Complicated Song (Parody of \"Complicated\" by Avril Lavigne) R","label_available":true},{"id":"363","name":"Adrenaline Mob Radio","label_available":true},{"id":"366","name":"Another Escape Radio","label_available":true},{"id":"376","name":"Astronaut In The Ocean (Remix) (feat. Egor Kreed) Radio","label_available":true},{"id":"458","name":"Astronaut In The Ocean (Remix) (feat. G-Eazy & DDG) Radio","label_available":true},{"id":"384","name":"Avenged Sevenfold Radio","label_available":true},{"id":"364","name":"Away (Live) Radio","label_available":true},{"id":"413","name":"Bad Words Radio","label_available":true},{"id":"388","name":"Believer Radio","label_available":true},{"id":"439","name":"Believer Radio","label_available":true},{"id":"496","name":"Best Friend (feat. Eminem) Radio","label_available":true},{"id":"427","name":"Beyoncé Radio","label_available":true},{"id":"437","name":"Bloodline Radio","label_available":true},{"id":"474","name":"Blues Traveler Radio","label_available":true},{"id":"508","name":"Bohemian Rhapsody (Remastered 2011) Radio","label_available":true},{"id":"402","name":"Breaking Benjamin Radio","label_available":true},{"id":"431","name":"C418 Radio","label_available":true},{"id":"476","name":"Calming Instrumental Covers Radio","label_available":true},{"id":"480","name":"Carousel Radio","label_available":true},{"id":"488","name":"Chris Snelling, Chris Mercer, Jonathan Sarlat, Nils Hahn, Robin","label_available":true},{"id":"456","name":"Christmas Radio","label_available":true},{"id":"373","name":"DJ Bobo Radio","label_available":true},{"id":"432","name":"David Gossage, Patrick Graham, Nelson Carter, Sean Dagher & Lin","label_available":true},{"id":"369","name":"Dinosaur Pile-Up Radio","label_available":true},{"id":"403","name":"Disturbed Radio","label_available":true},{"id":"367","name":"Don't Say Goodnight (It's Time for Love) (Part I & 2) Radio","label_available":true},{"id":"360","name":"Elton John Radio","label_available":true},{"id":"499","name":"Face Off Radio","label_available":true},{"id":"433","name":"Father & Son (with Paul Shaffer and the Hall of Fame Orchestra)","label_available":true},{"id":"400","name":"Fight The Fury Radio","label_available":true},{"id":"467","name":"Fire From The Gods Radio","label_available":true},{"id":"440","name":"First Of May Radio","label_available":true},{"id":"465","name":"Five Finger Death Punch Radio","label_available":true},{"id":"446","name":"From Ashes to New Radio","label_available":true},{"id":"435","name":"Fucked with an Anchor Radio","label_available":true},{"id":"416","name":"Future Radio","label_available":true},{"id":"493","name":"GIVE HEAVEN SOME HELL Radio","label_available":true},{"id":"378","name":"Garden Of Delight Radio","label_available":true},{"id":"383","name":"Garden Of Delight Radio","label_available":true},{"id":"421","name":"Gasolina Radio","label_available":true},{"id":"423","name":"Geek Music Radio","label_available":true},{"id":"397","name":"Ghost B.C. Radio","label_available":true},{"id":"500","name":"Goodness Gracious Radio","label_available":true},{"id":"459","name":"HARDY Radio","label_available":true},{"id":"471","name":"Halloween Radio","label_available":true},{"id":"463","name":"Hard Rock Radio","label_available":true},{"id":"512","name":"Heavy Metal Radio","label_available":true},{"id":"365","name":"Helloween Radio","label_available":true},{"id":"359","name":"Highly Suspect Radio","label_available":true},{"id":"478","name":"Hoist The Colours Radio","label_available":true},{"id":"417","name":"Hollywood Undead Radio","label_available":true},{"id":"362","name":"Hope Radio","label_available":true},{"id":"390","name":"I Never Lie Radio","label_available":true},{"id":"441","name":"I Prevail Radio","label_available":true},{"id":"448","name":"I Wanna Sex You Up (Re-Recorded - Slowed + Reverb) Radio","label_available":true},{"id":"449","name":"I'm A Boy (40th Anniversary Version - Live At Leeds) Radio","label_available":true},{"id":"407","name":"IShowSpeed Radio","label_available":true},{"id":"462","name":"Imagine Dragons Radio","label_available":true},{"id":"393","name":"Incubus Radio","label_available":true},{"id":"514","name":"Instrumental Radio","label_available":true},{"id":"357","name":"Instrumental Radio","label_available":true},{"id":"428","name":"Instrumental music for a good reading Radio","label_available":true},{"id":"377","name":"It's the Milkman Radio","label_available":true},{"id":"411","name":"JIM BOB Radio","label_available":true},{"id":"438","name":"Jack Sparrow Radio","label_available":true},{"id":"486","name":"Jim Bob Radio","label_available":true},{"id":"419","name":"Jump Up Radio","label_available":true},{"id":"443","name":"Jumps the Gun Radio","label_available":true},{"id":"399","name":"KIDZ BOP Kids (Children's) Radio","label_available":true},{"id":"468","name":"Karnivool Radio","label_available":true},{"id":"398","name":"Killed A Man Radio","label_available":true},{"id":"451","name":"Lucero Radio","label_available":true},{"id":"445","name":"MONSTERS Radio","label_available":true},{"id":"511","name":"Max Arnald, Chris Mercer, Yann Nyman, Django Wallace, Paula Kie","label_available":true},{"id":"430","name":"Me and My Brother Radio","label_available":true},{"id":"379","name":"Miles Away Radio","label_available":true},{"id":"495","name":"Miles On It Radio","label_available":true},{"id":"479","name":"Milkman Radio","label_available":true},{"id":"415","name":"Misery (Acoustic Version) Radio","label_available":true},{"id":"375","name":"Misery (Remastered 2009) Radio","label_available":true},{"id":"509","name":"Misery Radio","label_available":true},{"id":"481","name":"Monkey D. Luffy Sings A Song Radio","label_available":true},{"id":"391","name":"Moon Radio","label_available":true},{"id":"491","name":"Moonlight Radio","label_available":true},{"id":"420","name":"Morgan Wallen Radio","label_available":true},{"id":"386","name":"Mr. Ray & The Little Sunshine Kids (Children's) Radio","label_available":true},{"id":"408","name":"Naughty Girl (Live) Radio","label_available":true},{"id":"473","name":"Neighbors Radio","label_available":true},{"id":"396","name":"O.A.R. Radio","label_available":true},{"id":"404","name":"Otep Radio","label_available":true},{"id":"424","name":"Past Lives Radio","label_available":true},{"id":"382","name":"Pirate Radio Radio","label_available":true},{"id":"414","name":"Pirate Rap Radio","label_available":true},{"id":"501","name":"Pirate Song Radio","label_available":true},{"id":"513","name":"Pirates Of The Caribbean - Main Theme - He's A Pirate Radio","label_available":true},{"id":"506","name":"Play My Music Radio","label_available":true},{"id":"410","name":"Put Some Miles on It Radio","label_available":true},{"id":"507","name":"ROCKSTAR (feat. Roddy Ricch) Radio","label_available":true},{"id":"436","name":"ROCKSTAR Radio","label_available":true},{"id":"358","name":"Ra Radio","label_available":true},{"id":"442","name":"Radioactive (feat. Kendrick Lamar) Radio","label_available":true},{"id":"464","name":"Ransom Radio","label_available":true},{"id":"361","name":"Rare Americans Radio","label_available":true},{"id":"371","name":"Red Sun Rising Radio","label_available":true},{"id":"505","name":"Relaxing Stress Relief Meditation Music 🌸🧘‍♀️ Radio","label_available":true},{"id":"412","name":"Rival Sons Radio","label_available":true},{"id":"503","name":"Rockstar (with Bailey Zimmerman) (Live From Nashville) Radio","label_available":true},{"id":"380","name":"SAD! Radio","label_available":true},{"id":"460","name":"SOLD OUT Radio","label_available":true},{"id":"497","name":"Save Me (with Lainey Wilson) Radio","label_available":true},{"id":"461","name":"Scary Halloween Music Radio","label_available":true},{"id":"452","name":"Shaman's Harvest Radio","label_available":true},{"id":"504","name":"Shower Radio","label_available":true},{"id":"470","name":"Silly Pirate Song Radio","label_available":true},{"id":"466","name":"Skillet Radio","label_available":true},{"id":"401","name":"Slipknot Radio","label_available":true},{"id":"482","name":"So Cold Radio","label_available":true},{"id":"370","name":"Something Wicked Radio","label_available":true},{"id":"515","name":"Songs Like This Radio","label_available":true},{"id":"426","name":"Soothing Music Specialists Radio","label_available":true},{"id":"516","name":"Starset Radio","label_available":true},{"id":"475","name":"Stronger (What Doesn't Kill You) Radio","label_available":true},{"id":"374","name":"Sublime Radio","label_available":true},{"id":"498","name":"TEXAS HOLD 'EM Radio","label_available":true},{"id":"405","name":"TRUCK BED Radio","label_available":true},{"id":"453","name":"Team Radio","label_available":true},{"id":"484","name":"That Should Be Me Radio","label_available":true},{"id":"434","name":"That's What I Like (PARTYNEXTDOOR Remix) Radio","label_available":true},{"id":"494","name":"That's What I Like Radio","label_available":true},{"id":"425","name":"That's the Way (I Like It) (2004 Remaster) Radio","label_available":true},{"id":"492","name":"The Devil in I Radio","label_available":true},{"id":"368","name":"The Last Song Radio","label_available":true},{"id":"450","name":"The Milkman Radio","label_available":true},{"id":"469","name":"The Muffin Man (Sing-Along) (Instrumental) Radio","label_available":true},{"id":"385","name":"The Muffin Man Radio","label_available":true},{"id":"483","name":"The One With You Radio","label_available":true},{"id":"490","name":"The Struts Radio","label_available":true},{"id":"447","name":"Theme From Mahogany (Do You Know Where You're Going To) (Altern","label_available":true},{"id":"381","name":"Thumbprint Radio","label_available":true},{"id":"444","name":"Tic Tac Toe (feat. Kodak Black) Radio","label_available":true},{"id":"422","name":"Tic Tac Toe Radio","label_available":true},{"id":"454","name":"Tool Radio","label_available":true},{"id":"485","name":"Trap Queen (feat. Quavo & Gucci Mane) Radio","label_available":true},{"id":"477","name":"Twelve Foot Ninja Radio","label_available":true},{"id":"517","name":"Twenty One Pilots Radio","label_available":true},{"id":"409","name":"Unsainted Radio","label_available":true},{"id":"502","name":"Various Artists Radio","label_available":true},{"id":"472","name":"Volbeat Radio","label_available":true},{"id":"406","name":"Waka Waka (This Time for Africa) [The Official 2010 FIFA World","label_available":true},{"id":"356","name":"Warriors Radio","label_available":true},{"id":"510","name":"Wellerman Radio","label_available":true},{"id":"429","name":"You and Me Radio","label_available":true},{"id":"372","name":"You're Welcome Radio","label_available":true},{"id":"418","name":"You've Got a Friend in Me (-KINGDOM HEARTS III Version-) Radio","label_available":true},{"id":"394","name":"ZAYN Radio","label_available":true},{"id":"389","name":"jobu109's QuickMix","label_available":true},{"id":"489","name":"time of night Radio","label_available":true},{"id":"487","name":"wish it was me Radio","label_available":true}]},"metadata":{"title":"Loosing Interest (feat. Shiloh Dynasty)","artist":"timmies","album":"Passion & Confusion (feat. Shiloh Dynasty)","station":"SAD! Radio"}};
;
(function (global) {
  "use strict";

  const commandJoin = "101";
  const stateJoin = "102";
  let sequence = 0;

  function create(com, config) {
    const roomId = config.roomId;
    const send = config.send, receive = config.receive;
    const virtualSubscriptions = new Map();
    const physicalSubscriptions = [];
    let lastSource = "", active = false, connected = false, started = false, nextId = 0;

    function emit(type, join, value) {
      for (const item of virtualSubscriptions.values()) {
        if (item.type === type && item.join === join) item.callback(value);
      }
    }
    function watch(type, join, callback) {
      physicalSubscriptions.push({ type, join, id: com.subscribeState(type, join, callback) });
    }
    function request(op, fields) {
      if (!active || !connected) return;
      const json = JSON.stringify({ ...fields, op, room_id: roomId, q: ++sequence });
      if (json.length <= 255) com.publishEvent("s", commandJoin, json);
    }
    const pages = new Map([
      [send.audioPage, "audio_page"], [send.videoPage, "video_page"],
      [send.mediaPage, "media_page"], [send.nowPlayingPage, "playing_page"], [send.volume, "volume"]
    ]);
    const keys = new Map(Object.entries(send.transport).map(([key, join]) => [join, key]));
    const operations = new Map([
      [send.muteOn, "mute_on"], [send.muteOff, "mute_off"], [send.off, "off"]
    ]);
    const stateFields = [
      ["volume", "n", receive.volume], ["volumeValid", "b", receive.volumeValid],
      ["muted", "b", receive.muted], ["muteValid", "b", receive.sources.Mute_Valid_FB],
      ["power", "b", receive.power], ["sourceValid", "b", receive.sourceValid],
      ["mediaValid", "b", receive.mediaValid], ["selectReady", "b", receive.selectReady],
      ["nowPlayingValid", "b", receive.nowPlayingValid],
      ["audioIndex", "n", receive.sources.Audio_Source_Selected_FB],
      ["videoIndex", "n", receive.sources.Video_Source_Selected_FB],
      ["audioValid", "b", receive.sources.Audio_Source_Selected_Valid_FB],
      ["videoValid", "b", receive.sources.Video_Source_Selected_Valid_FB]
    ];
    let state = new Map();
    let deviceCatalog = [];
    const deviceValues = new Map();
    const deviceWire = new global.RoomSerialWire.Collector(roomId, "d");
    function publishDevices() {
      onDevices(deviceCatalog.map(device => ({ ...device, value: deviceValues.get(device.id) ?? null })));
    }
    let onDevices = () => {};
    const streams = [receive.audioSources, receive.videoSources, receive.mediaList, receive.nowPlaying, receive.progress, receive.transport];

    return {
      subscribeState(type, join, callback) {
        const id = ++nextId;
        virtualSubscriptions.set(id, { type, join: String(join), callback });
        return id;
      },
      unsubscribeState(type, join, id) { virtualSubscriptions.delete(id); },
      onDeviceFeedback(callback) { onDevices = callback; },
      sendDevice(device_id, action, value) {
        const device = deviceCatalog.find(item => item.id === device_id);
        if (!device || (action !== "level" && (!device.shade || !["open", "close", "stop"].includes(action)))) return;
        if (action === "level" && (!Number.isInteger(value) || value < 0 || value > 100)) return;
        request("device", { device_id, action, value });
      },
      start() {
        if (started) return;
        started = active = true;
        watch("s", stateJoin, value => {
          let packet;
          try { packet = JSON.parse(value); } catch (_) { return; }
          if (!active || !packet || packet.r !== roomId) return;
          if (packet.k === "d") {
            let result;
            try { result = deviceWire.accept(packet); } catch (_) { return; }
            if (result?.requestPage) request("device_page", { value: result.requestPage });
            if (Array.isArray(result?.packet?.devices)) {
              deviceCatalog = result.packet.devices.filter(device => device && Number.isInteger(device.id) &&
                device.id > 0 && typeof device.name === "string" && typeof device.shade === "boolean");
              for (const device of deviceCatalog) if (!deviceValues.has(device.id)) deviceValues.set(device.id, device.value);
              publishDevices();
            }
            return;
          }
          if (Number.isInteger(packet.u) && packet.u > 0 && (packet.v === null ||
              (Number.isInteger(packet.v) && packet.v >= 0 && packet.v <= 100))) {
            deviceValues.set(packet.u, packet.v);
            if (deviceCatalog.some(device => device.id === packet.u)) publishDevices();
            return;
          }
          for (const [key, type, join] of stateFields) {
            if (!Object.hasOwn(packet, key)) continue;
            const value = type === "b" ? packet[key] === 1 : packet[key];
            if (state.has(key) && state.get(key) === value) continue;
            state.set(key, value);
            emit(type, join, value);
          }
        });
        streams.forEach((join, index) => watch("s", String(index + 103), value => {
          let packet;
          try { packet = JSON.parse(value); } catch (_) { return; }
          if (active && packet && (packet.r ?? packet.room_id) === roomId) emit("s", join, value);
        }));
        watch("b", receive.connected, value => {
          if (!active || connected === Boolean(value)) return;
          connected = Boolean(value);
          if (!connected) {
            state = new Map();
            deviceValues.clear();
            publishDevices();
          }
          emit("b", receive.connected, connected);
          if (connected) {
            deviceWire.reset();
            request("room");
            if (deviceWire.pending) request("device_page", { value: deviceWire.pending });
          }
        });
      },
      publishEvent(type, join, value) {
        if (type === "s" && join === send.sourceId) { lastSource = String(value); return; }
        if (type === "s" && join === send.mediaRequest) {
          try { request("item", JSON.parse(value)); } catch (_) {}
          return;
        }
        if (type === "n" && pages.has(join)) {
          if (value || join === send.volume) request(pages.get(join), { value });
          return;
        }
        if (type !== "b") return;
        if (join === send.shuffle) { request("shuffle", { value: value ? 1 : 0 }); return; }
        if (!value) return;
        if (join === send.selectAudio || join === send.selectVideo) {
          if (lastSource) request(join === send.selectAudio ? "audio" : "video", { value: lastSource });
        } else if (operations.has(join)) request(operations.get(join));
        else if (keys.has(join)) request("key", { value: keys.get(join) });
      },
      subscriptionCount() { return physicalSubscriptions.length; },
      destroy() {
        active = false;
        for (const item of physicalSubscriptions) com.unsubscribeState(item.type, item.join, item.id);
        physicalSubscriptions.length = 0;
        virtualSubscriptions.clear();
      }
    };
  }

  global.PanelRoomTransport = { create };
})(window);

;
(function(global) {
  "use strict";

  function mount(parent, send) {
    const panel = document.createElement("details");
    panel.className = "rm-devices";
    panel.hidden = true;
    const summary = document.createElement("summary");
    summary.textContent = "Lighting and shades";
    panel.append(summary);
    const controls = new Map();

    function addDevice({id, name, shade}) {
      const special = shade ? [["Open", "open"], ["Close", "close"], ["Stop", "stop"]] :
        [["On", "level", 100], ["Off", "level", 0]];
      const group = document.createElement("fieldset");
      const title = document.createElement("legend");
      title.textContent = name;
      const actual = document.createElement("p");
      actual.setAttribute("role", "status");
      actual.textContent = "Awaiting Control4 feedback";
      group.append(title, actual);
      function button(label, action, value) {
        const control = document.createElement("button");
        control.type = "button";
        control.textContent = label;
        control.onclick = () => send(id, action, typeof value === "function" ? value() : value);
        group.append(control);
      }
      for (const [label, action, value] of special) button(label, action, value);
      const label = document.createElement("label");
      label.textContent = "Target %";
      const target = document.createElement("input");
      target.type = "number";
      target.min = "0";
      target.max = "100";
      target.step = "1";
      target.value = "50";
      label.append(target);
      group.append(label);
      button("Apply", "level", () => target.valueAsNumber);
      group.disabled = true;
      controls.set(id, { group, actual });
      panel.append(group);
    }

    parent.append(panel);

    return function update(connected, devices) {
      panel.hidden = devices.length === 0;
      const ids = new Set(devices.map(device => device.id));
      for (const [id, control] of controls) {
        if (ids.has(id)) continue;
        control.group.remove();
        controls.delete(id);
      }
      for (const device of devices) {
        if (!controls.has(device.id)) addDevice(device);
        const { group, actual } = controls.get(device.id);
        group.disabled = !connected;
        actual.textContent = !connected ? "Control4 disconnected" :
          Number.isInteger(device.value) ? (device.shade ? "Position " : "Level ") + device.value + "%" : "Awaiting Control4 feedback";
      }
    };
  }

  global.PanelRoomDevices = { mount };
})(window);

;
(function(global){
  'use strict';
  if(global.RoomMediaPages)return;
  const views=new Map(),sessions=new Map();
  let selectedRoom=null,observer=null,scheduled=false,initializationTimer=null,initializationAttempts=0;
  let routerRooms=[], directory=null, sequence=0, homeView='home';
  const destinations = [
    ['home', 'Home'], ['rooms', 'Rooms'], ['lights', 'Lights'], ['shades', 'Shades'],
    ['security', 'Security'], ['comfort', 'Comfort'], ['cameras', 'Cameras']
  ];
  const sceneViews = new Set(['lights', 'shades']);
  const iconPaths = {
    home:'M3 11l9-8 9 8 M5 10v10h5v-6h4v6h5V10',
    rooms:'M3 4h18v16H3z M12 4v16 M3 12h9 M12 9h9',
    lights:'M8 16c0-2-3-3-3-7a7 7 0 0 1 14 0c0 4-3 5-3 7 M8 17h8 M9 20h6 M11 22h2',
    shades:'M4 4h16v16H4z M4 8h16 M4 11h16 M4 14h16 M4 17h16',
    security:'M12 3l8 3v5c0 5-3 8-8 11-5-3-8-6-8-11V6z M9 12l2 2 4-4',
    comfort:'M9 14V6a3 3 0 0 1 6 0v8a5 5 0 1 1-6 0z M12 9v8 M18 8h3 M18 12h2',
    cameras:'M3 6h12v11H3z M15 10l6-3v9l-6-3'
  };
  function icon(doc,id){
    const canvas=doc.createElement('canvas');
    canvas.width=48;canvas.height=48;canvas.className='c4-dock-icon';canvas.setAttribute('aria-hidden','true');
    const pen=canvas.getContext('2d');
    pen.scale(2,2);pen.lineWidth=1.4;pen.lineCap='round';pen.lineJoin='round';pen.strokeStyle='#f5f8fc';
    pen.stroke(new Path2D(iconPaths[id]));
    return canvas;
  }
  function openView(view){
    if(homeView===view)return;
    homeView=view;
    remountViews();
  }
  function mountHome(root, original){
    const previousClass=root.className;
    root.className='c4-home';
    root.innerHTML=`
      <header class="c4-home-header">
        <button class="c4-home-brand" type="button" data-home-view="home">Home</button>
        <time class="c4-home-clock"><span class="c4-home-time"></span><span class="c4-home-date"></span></time>
      </header>
      <main class="c4-home-main">
        <div class="c4-home-content"></div>
        <nav class="c4-home-nav" aria-label="Whole home"></nav>
      </main>`;
    const doc=root.ownerDocument;
    const content=root.querySelector('.c4-home-content');
    const nav=root.querySelector('.c4-home-nav');
    function button(title,description,action,selected,dockId){
      const node=doc.createElement('button');node.type='button';
      if(dockId)node.append(icon(doc,dockId));
      const heading=doc.createElement('strong');heading.textContent=title;
      node.append(heading);
      if(description){const sub=doc.createElement('span');sub.textContent=description;node.append(sub);}
      if(selected)node.setAttribute('aria-current','page');
      node.onclick=action;
      return node;
    }
    root.querySelector('[data-home-view]').onclick=()=>openView('home');
    for(const [id,title] of destinations)
      nav.append(button(title,'',()=>openView(id),homeView===id,id));
    if(homeView==='rooms'){
      const title=doc.createElement('h1');title.textContent='Rooms';content.append(title);
      const list=doc.createElement('div');list.className='c4-home-rooms';content.append(list);
      const current=room();
      if(current)list.append(button('Open '+current.name,'',()=>selectRoom(current.id)));
      for(const entry of rooms())if(entry.id!==current?.id)
        list.append(button(entry.name,'',()=>selectRoom(entry.id)));
      if(!current){const status=doc.createElement('p');status.textContent='Waiting for the Panel Room Router.';list.append(status);}
    }else{
      const item=destinations.find(([id])=>id===homeView);
      const title=doc.createElement('h1');title.textContent=item&&homeView!=='home'?item[1]:'Welcome home';content.append(title);
      if(sceneViews.has(homeView)){
        const heading=doc.createElement('h2');heading.textContent=homeView==='lights'?'Lighting scenes':'Shade scenes';
        const status=doc.createElement('p');status.setAttribute('role','status');
        status.textContent='No verified Control4 scenes are available.';
        content.append(heading,status);
        if(room())content.append(button('Open '+room().name,'',()=>selectRoom(room().id)));
      }else if(item&&homeView!=='home'){
        const status=doc.createElement('p');status.setAttribute('role','status');
        status.textContent=item[1]+' integration is unavailable.';
        content.append(status);
      }else{
        const summary=doc.createElement('p');summary.textContent=room()?'Rooms available':'Waiting for the Panel Room Router';content.append(summary);
        if(room())content.append(button('Open '+room().name,'',()=>selectRoom(room().id)));
      }
    }
    const clock=root.querySelector('.c4-home-clock');
    function updateClock(){
      const now=new Date();
      clock.dateTime=now.toISOString();
      clock.querySelector('.c4-home-time').textContent=new Intl.DateTimeFormat(undefined,{hour:'numeric',minute:'2-digit'}).format(now);
      clock.querySelector('.c4-home-date').textContent=new Intl.DateTimeFormat(undefined,{weekday:'long',month:'long',day:'numeric'}).format(now);
    }
    updateClock();
    const timer=global.setInterval(updateClock,15000);
    return {destroy(){global.clearInterval(timer);root.className=previousClass;root.innerHTML=original;}};
  }
  function rooms(){return global.ROOM_MEDIA_CONFIG?.enabled ? routerRooms : global.ROOM_MEDIA_ROOMS||[];}
  function connectDirectory(){
    if(directory||!global.ROOM_MEDIA_CONFIG?.enabled)return;
    const com=getCom();if(!com)return;
    const collector=new global.RoomSerialWire.Collector(0,'rooms');
    const subscriptions=[];
    const current={close(){for(const [type,join,id] of subscriptions)com.unsubscribeState(type,join,id);}};
    directory=current;
    let online=false;
    function request(op,value){
      queueMicrotask(()=>{if(directory===current&&online)com.publishEvent('s','101',JSON.stringify({op,value,q:++sequence}));});
    }
    function watch(type,join,callback){subscriptions.push([type,join,com.subscribeState(type,join,callback)]);}
    watch('s','102',value=>{
      if(!online||directory!==current)return;
      let result;
      try{result=collector.accept(JSON.parse(value));}catch(_){return;}
      if(result?.requestPage)request('rooms_page',result.requestPage);
      if(!Array.isArray(result?.packet?.rooms))return;
      const seen=new Set();
      const next=result.packet.rooms.filter(item=>item&&Number.isInteger(item.id)&&item.id>0&&item.id<=65535&&
        typeof item.name==='string'&&item.name.trim()&&!seen.has(item.id)&&seen.add(item.id))
        .map(item=>({...item,config:global.ROOM_MEDIA_CONFIG}));
      if(JSON.stringify(next.map(({id,name})=>[id,name]))===JSON.stringify(routerRooms.map(({id,name})=>[id,name])))return;
      routerRooms=next;
      queueMicrotask(()=>{if(directory===current)remountViews();});
    });
    watch('b',global.ROOM_MEDIA_CONFIG.receive.connected,value=>{
      if(online===Boolean(value))return;
      online=Boolean(value);
      collector.reset();
      if(online)request('rooms');
    });
  }
  function room(){return rooms().find(r=>r.id===selectedRoom)||rooms()[0];}
  function getCom(){
    const frames=[global];try{frames.push(global.parent);}catch(_){}try{frames.push(global.top);}catch(_){}
    for(const frame of frames){try{if(frame.CrComLib&&typeof frame.CrComLib.subscribeState==='function'&&typeof frame.CrComLib.publishEvent==='function')return frame.CrComLib;}catch(_){}}
    return null;
  }
  function session(entry){
    if(sessions.has(entry.id))return sessions.get(entry.id);
    const com=getCom();if(!com)return null;
    const config={...entry.config,roomId:entry.id,browser:false};
    const transport=global.PanelRoomTransport.create(com,config);
    const bridge=new global.CrestronRoomBridge(transport,config);
    const destroy=bridge.destroy.bind(bridge);
    bridge.destroy=()=>{destroy();transport.destroy();};
    const value={listeners:new Set(),state:{connected:false,selectionReady:false},bridge,transport};
    sessions.set(entry.id,value);
    bridge.subscribe(update=>{Object.assign(value.state,update);for(const listener of value.listeners)listener(update);});
    transport.onDeviceFeedback(devices=>{value.state.devices=devices;for(const listener of value.listeners)listener({devices});});
    transport.start();
    return value;
  }
  function facade(entry){
    const shared=session(entry);if(!shared)return null;
    let listener=null,closed=false;
    const adapter={subscribe(fn){listener=fn;shared.listeners.add(fn);fn(shared.state);},destroy(){if(closed)return;closed=true;if(listener)shared.listeners.delete(listener);if(!shared.listeners.size){shared.bridge.destroy();sessions.delete(entry.id);}}};
    for(const method of ['selectSource','selectItem','transport','volume','mute','off','shuffle'])adapter[method]=(...args)=>{if(!closed)return shared.bridge[method](...args);};
    adapter.sendDevice=(...args)=>{if(!closed)shared.transport.sendDevice(...args);};
    return adapter;
  }
  function mount(root){
    if(views.has(root))return;
    const original=root.innerHTML;
    const kind=root.dataset.c4Page;
    if(homeView!=='room'){
      views.set(root,mountHome(root,original));
      return;
    }
    const entry=room();if(!entry)return false;
    const live=!!entry.config.enabled;
    const bridge=live?facade(entry):null;
    if(live&&!bridge){
      const note=root.querySelector('[data-startup-status]');if(note&&note.textContent!=='Waiting for Construct communication. Layout preview only.')note.textContent='Waiting for Construct communication. Layout preview only.';
      return false; // Bounded initialization retries below; no network polling.
    }
    const view=global.RoomMediaApp.mount(root,{bridge,data:live?null:global.ROOM_MEDIA_PREVIEW,initialMode:kind==='watch'?'watch':'listen',rooms:rooms(),roomId:entry.id,onRoomChange:selectRoom});
    const header=root.querySelector('.rm-header');
    if(header){
      const home=root.ownerDocument.createElement('button');
      home.type='button';home.className='c4-room-home';home.textContent='Home';
      home.onclick=()=>openView('home');header.prepend(home);
    }
    views.set(root,{destroy(){view.destroy();root.innerHTML=original;}});
  }
  function attached(root){
    if(!root.isConnected)return false;
    try{const frame=root.ownerDocument.defaultView.frameElement;return !frame||frame.isConnected;}catch(_){return true;}
  }
  function detach(root){const view=views.get(root);if(view){view.destroy();views.delete(root);}}
  function scan(){
    scheduled=false;
    connectDirectory();
    for(const [root,view]of views)if(!attached(root)){view.destroy();views.delete(root);}
    let waiting=false;global.document.querySelectorAll('[data-c4-page]').forEach(root=>{if(mount(root)===false)waiting=true;});
    if(waiting&&!initializationTimer&&initializationAttempts<30)initializationTimer=setTimeout(()=>{initializationTimer=null;initializationAttempts++;scan();},100);
    if(!waiting){clearTimeout(initializationTimer);initializationTimer=null;initializationAttempts=0;}
  }
  function remountViews(){
    const previousRoots=Array.from(views.keys());
    for(const view of views.values())view.destroy();views.clear();
    for(const root of previousRoots)if(attached(root))mount(root);
    scan();
  }
  function selectRoom(id){
    if(!rooms().some(r=>r.id===id)||(id===room()?.id&&homeView==='room'))return;
    selectedRoom=id;homeView='room';
    remountViews();
    global.dispatchEvent(new CustomEvent('room-media:room-changed',{detail:{roomId:id}}));
  }
  function start(){
    if(!observer){observer=new MutationObserver(()=>{if(!scheduled){scheduled=true;queueMicrotask(scan);}});observer.observe(global.document.documentElement,{childList:true,subtree:true});}
    scan();
  }
  function destroy(){if(directory){const old=directory;directory=null;old.close();}routerRooms=[];clearTimeout(initializationTimer);initializationTimer=null;initializationAttempts=0;if(observer)observer.disconnect();observer=null;for(const view of views.values())view.destroy();views.clear();for(const value of sessions.values())value.bridge.destroy();sessions.clear();}
  global.RoomMediaPages={start,destroy,selectRoom,attach(root){mount(root);start();},detach,stats(){return {views:views.size,sessions:sessions.size,subscriptions:Array.from(sessions.values()).reduce((n,s)=>n+s.transport.subscriptionCount(),0)};}};
  global.addEventListener('pagehide',destroy);
  global.addEventListener('pageshow',start);
})(window);
