﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

// Class to manage all data for the current quest
public class QuestData
{
    // All components in the quest
    public Dictionary<string, QuestComponent> components;

    // A list of flags that have been set during the quest
    public HashSet<string> flags;

    // A dictionary of heros that have been selected in events
    public Dictionary<string, List<Round.Hero>> heroSelection;

    // List of ini files containing quest data
    List<string> files;

    // Location of the quest.ini file
    public string questPath = "";

    // Data from 'Quest' section
    public Quest quest;

    Game game;

    public QuestData(QuestLoader.Quest q)
    {
        questPath = q.path + "/quest.ini";
        LoadQuestData();
    }

    // Read all data files and populate components for quest
    public QuestData(string path)
    {
        questPath = path;
        LoadQuestData();
    }

    public void LoadQuestData()
    {
        Debug.Log("Loading quest from: \"" + questPath + "\"" + System.Environment.NewLine);
        game = Game.Get();

        components = new Dictionary<string, QuestComponent>();
        flags = new HashSet<string>();
        heroSelection = new Dictionary<string, List<Round.Hero>>();

        // Read the main quest file
        IniData d = IniRead.ReadFromIni(questPath);
        // Failure to read quest is fatal
        if(d == null)
        {
            Debug.Log("Failed to load quest from: \"" + questPath + "\"");
            Application.Quit();
        }

        // List of data files
        files = new List<string>();
        // The main data file is included
        files.Add(questPath);

        // Find others (no addition files is not fatal)
        if(d.Get("QuestData") != null)
        {
            foreach (string file in d.Get("QuestData").Keys)
            {
                // path is relative to the main file (absolute not supported)
                files.Add(Path.GetDirectoryName(questPath) + "/" + file);
            }
        }

        foreach (string f in files)
        {
            // Read each file
            d = IniRead.ReadFromIni(f);
            // Failure to read a file is fatal
            if (d == null)
            {
                Debug.Log("Unable to read quest file: \"" + f + "\"");
                Application.Quit();
            }
            foreach (KeyValuePair<string, Dictionary<string, string>> section in d.data)
            {
                // Add the section to our quest data
                AddData(section.Key, section.Value, Path.GetDirectoryName(f));
            }
        }
    }

    // Add a section from an ini file to the quest data.  Duplicates are not allowed
    void AddData(string name, Dictionary<string, string> content, string path)
    {
        // Fatal error on duplicates
        if(components.ContainsKey(name))
        {
            Debug.Log("Duplicate component in quest: " + name);
            Application.Quit();
        }

        if (name.Equals("Quest"))
        {
            quest = new Quest(content);
        }
        // Check for known types and create
        if (name.IndexOf(Tile.type) == 0)
        {
            Tile c = new Tile(name, content);
            components.Add(name, c);
        }
        if (name.IndexOf(Door.type) == 0)
        {
            Door c = new Door(name, content, game);
            components.Add(name, c);
        }
        if (name.IndexOf(Token.type) == 0)
        {
            Token c = new Token(name, content, game);
            components.Add(name, c);
        }
        if (name.IndexOf(Event.type) == 0)
        {
            Event c = new Event(name, content);
            components.Add(name, c);
        }
        if (name.IndexOf(Monster.type) == 0)
        {
            Monster c = new Monster(name, content, game);
            components.Add(name, c);
        }
        if (name.IndexOf(MPlace.type) == 0)
        {
            MPlace c = new MPlace(name, content);
            components.Add(name, c);
        }
        // If not known ignore
    }

    // Class for Tile components (use TileSide content data)
    public class Tile : QuestComponent
    {
        new public static string type = "Tile";
        public int rotation = 0;
        public string tileSideName;

        public Tile(string s) : base(s)
        {
            locationSpecified = true;
            typeDynamic = type;
            Game game = Game.Get();
            foreach (KeyValuePair<string, TileSideData> kv in game.cd.tileSides)
            {
                tileSideName = kv.Key;
            }
        }

        public Tile(string name, Dictionary<string, string> data) : base(name, data)
        {
            locationSpecified = true;
            typeDynamic = type;
            // Get rotation if specified
            if (data.ContainsKey("rotation"))
            {
                rotation = int.Parse(data["rotation"]);
            }

            // Find the tileside that is used
            if (data.ContainsKey("side"))
            {
                tileSideName = data["side"];
                // 'TileSide' prefix is optional, test both
                if (tileSideName.IndexOf("TileSide") != 0)
                {
                    tileSideName = "TileSide" + tileSideName;
                }
            }
            else
            {
                // Fatal if missing
                Debug.Log("Error: No TileSide specified in quest component: " + name);
                Application.Quit();
            }
        }

        override public string ToString()
        {
            string nl = System.Environment.NewLine;
            string r = base.ToString();

            r += "side=" + tileSideName + nl;
            if (rotation != 0)
            {
                r += "rotation=" + rotation + nl;
            }
            return r;
        }
    }

    // Doors are like tokens but placed differently and have different defaults
    public class Door : Event
    {
        new public static string type = "Door";
        public int rotation = 0;
        public GameObject gameObject;
        public string colourName = "white";

        public Door(string s) : base(s)
        {
            locationSpecified = true;
            typeDynamic = type;
            text = "You can open this door with an \"Open Door\" action.";
            cancelable = true;
        }

        public Door(string name, Dictionary<string, string> data, Game game) : base(name, data)
        {
            locationSpecified = true;
            typeDynamic = type;
            // Doors are cancelable because you can select then cancel
            cancelable = true;

            if (data.ContainsKey("rotation"))
            {
                rotation = int.Parse(data["rotation"]);
            }

            // color is only supported as a hexadecimal "#RRGGBB" format
            if (data.ContainsKey("color"))
            {
                colourName = data["color"];
            }

            if (text.Equals(""))
            {
                text = "You can open this door with an \"Open Door\" action.";
            }
        }

        override public string ToString()
        {
            string nl = System.Environment.NewLine;
            string r = base.ToString();

            if (!colourName.Equals("white"))
            {
                r += "color=" + colourName + nl;
            }
            if (rotation != 0)
            {
                r += "rotation=" + rotation + nl;
            }
            return r;
        }
    }

    // Tokens are events that are tied to a token placed on the board
    public class Token : Event
    {
        new public static string type = "Token";
        public string spriteName;

        public Token(string s) : base(s)
        {
            locationSpecified = true;
            typeDynamic = type;
            spriteName = "search-token";
            cancelable = true;
        }

        public Token(string name, Dictionary<string, string> data, Game game) : base(name, data)
        {
            locationSpecified = true;
            typeDynamic = type;
            // Tokens are cancelable because you can select then cancel
            cancelable = true;

            // default token type is search, this is the image asset name
            spriteName = "search-token";
            if (data.ContainsKey("type"))
            {
                spriteName = data["type"];
            }
        }

        override public string ToString()
        {
            string nl = System.Environment.NewLine;
            string r = base.ToString();

            if(!spriteName.Equals("search-token"))
            {
                r += "type=" + spriteName + nl;
            }
            return r;
        }
    }


    // Monster items are monster group placement events
    public class Monster : Event
    {
        new public static string type = "Monster";
        public MonsterData mData;
        public string[][] placement;
        public bool unique = false;
        public string uniqueTitle = "";
        public string uniqueTitleOriginal = "";
        public string uniqueText = "";
        public string[] mTypes;
        public string[] mTraits;

        public Monster(string s) : base(s)
        {
            locationSpecified = true;
            typeDynamic = type;
            Game game = Game.Get();
            foreach (KeyValuePair<string, MonsterData> kv in game.cd.monsters)
            {
                mData = kv.Value;
            }
            mTypes = new string[1];
            mTypes[0] = mData.sectionName;
            mTraits = new string[0];

            placement = new string[5][];
            for (int i = 0; i < placement.Length; i++)
            {
                placement[i] = new string[0];
            }
        }

        public Monster(string name, Dictionary<string, string> data, Game game) : base(name, data)
        {
            locationSpecified = true;
            typeDynamic = type;
            //First try to a list of specific types
            if (data.ContainsKey("monster"))
            {
                mTypes = data["monster"].Split(' ');
            }
            else
            {
                mTypes = new string[0];
            }

            // Next try to find a type that is valid
            foreach (string t in mTypes)
            {
                // Monster type must exist in content packs, 'Monster' is optional
                if (game.cd.monsters.ContainsKey(t) && mData == null)
                {
                    mData = game.cd.monsters[t];
                }
                else if (game.cd.monsters.ContainsKey("Monster" + t) && mData == null)
                {
                    mData = game.cd.monsters["Monster" + t];
                }
            }

            // If we didn't find anything try by trait
            mTraits = new string[0];
            if (mData == null)
            {
                if (data.ContainsKey("traits"))
                {
                    mTraits = data["traits"].Split(' ');
                }
                else
                {
                    Debug.Log("Error: Cannot find monster and no traits provided: " + data["monster"] + " specified in event: " + name);
                    Application.Quit();
                }

                List<MonsterData> list = new List<MonsterData>();
                foreach (KeyValuePair<string, MonsterData> kv in game.cd.monsters)
                {
                    bool allFound = true;
                    foreach (string t in mTraits)
                    {
                        bool found = false;
                        foreach (string mt in kv.Value.traits)
                        {
                            if (mt.Equals(t)) found = true;
                        }
                        if (found == false) allFound = false;
                    }
                    if (allFound)
                    {
                        list.Add(kv.Value);
                    }
                }

                // Not found, throw error
                if (list.Count == 0)
                {
                    Debug.Log("Error: Unable to find monster of traits specified in event: " + name);
                    Application.Quit();
                }

                mData = list[Random.Range(0, list.Count)];
            }
            text = text.Replace("{type}", mData.name);

            placement = new string[5][];
            for (int i = 0; i < placement.Length; i++)
            {
                placement[i] = new string[0];
                if (data.ContainsKey("placement" + i))
                {
                    placement[i] = data["placement" + i].Split(' ');
                }
            }

            if (data.ContainsKey("unique"))
            {
                unique = bool.Parse(data["unique"]);
            }
            if (data.ContainsKey("uniquetitle"))
            {
                uniqueTitleOriginal = data["uniquetitle"];
                uniqueTitle = uniqueTitleOriginal.Replace("{type}", mData.name);
            }
            if (uniqueTitle.Equals(""))
            {
                uniqueTitle = "Master " + mData.name;
            }
            if (data.ContainsKey("uniquetext"))
            {
                uniqueText = data["uniquetext"];
            }
        }

        override public void ChangeReference(string oldName, string newName)
        {
            for (int j = 0; j < placement.Length; j++)
            {
                for (int i = 0; i < placement[j].Length; i++)
                {
                    if (placement[j][i].Equals(oldName))
                    {
                        placement[j][i] = newName;
                    }
                }
                placement[j] = RemoveFromArray(placement[j], "");
            }
        }

        override public string ToString()
        {
            string nl = System.Environment.NewLine;
            string r = base.ToString();

            int textStart = r.IndexOf("text=");
            int textEnd = r.IndexOf("\n", textStart);
            r = r.Substring(0, textStart) + "text=\"" + originalText + "\"" + r.Substring(textEnd);

            if (mTypes.Length > 0)
            {
                r += "monster=";
                foreach (string s in mTypes)
                {
                    r += s + " ";
                }
                r = r.Substring(0, r.Length - 1) + nl;
            }
            if (mTraits.Length > 0)
            {
                r += "traits=";
                foreach (string s in mTraits)
                {
                    r += s + " ";
                }
                r = r.Substring(0, r.Length - 1) + nl;
            }
            for(int i = 0; i < placement.Length; i++)
            {
                if (placement[i].Length > 0)
                {
                    r += "placement" + i + "=";
                    foreach (string s in placement[i])
                    {
                        r += s + " ";
                    }
                    r = r.Substring(0, r.Length - 1) + nl;
                }
            }
            if(unique)
            {
                r += "unique=true" + nl;
            }
            if (!uniqueTitleOriginal.Equals(""))
            {
                r += "uniquetitle=\"" + uniqueTitleOriginal + "\"" + nl;
            }
            if (!uniqueText.Equals(""))
            {
                r += "uniquetext=\"" + uniqueText + "\"" + nl;
            }

            return r;
        }
    }


    // Events are used to create dialogs that control the quest
    public class Event : QuestComponent
    {
        new public static string type = "Event";
        public string text = "";
        public string originalText = "";
        public string confirmText = "";
        public string failText = "";
        public string trigger = "";
        public string[] nextEvent;
        public string[] failEvent;
        public string heroListName = "";
        public int gold = 0;
        public int minHeroes = 0;
        public int maxHeroes = 0;
        public string[] addComponents;
        public string[] removeComponents;
        public string[] flags;
        public string[] setFlags;
        public string[] clearFlags;
        public bool cancelable = false;
        public bool highlight = false;

        public Event(string s) : base(s)
        {
            typeDynamic = type;
            nextEvent = new string[0];
            failEvent = new string[0];
            addComponents = new string[0];
            removeComponents = new string[0];
            flags = new string[0];
            setFlags = new string[0];
            clearFlags = new string[0];
        }

        public Event(string name, Dictionary<string, string> data) : base(name, data)
        {
            typeDynamic = type;
            // Text to be displayed
            if (data.ContainsKey("text"))
            {
                text = data["text"];
            }
            originalText = text;

            if (data.ContainsKey("confirmtext"))
            {
                confirmText = data["confirmtext"];
            }

            if (data.ContainsKey("failtext"))
            {
                failText = data["failtext"];
            }

            // Should the target location by highlighted?
            if (data.ContainsKey("highlight"))
            {
                highlight = bool.Parse(data["highlight"]);
            }

            // Events to trigger on confirm or success
            if (data.ContainsKey("event"))
            {
                nextEvent = data["event"].Split(' ');
            }
            else
            {
                nextEvent = new string[0];
            }

            // Events to trigger on confirm or success
            if (data.ContainsKey("failevent"))
            {
                failEvent = data["failevent"].Split(' ');
            }
            else
            {
                failEvent = new string[0];
            }

            // Heros from another event can be hilighted
            if (data.ContainsKey("hero"))
            {
                heroListName = data["hero"];
            }

            // alter party gold (currently unused)
            if (data.ContainsKey("gold"))
            {
                gold = int.Parse(data["gold"]);
            }
            
            // minimum heros required to be selected for event
            if (data.ContainsKey("minhero"))
            {
                minHeroes = int.Parse(data["minhero"]);
            }

            // maximum heros selectable for event (0 disables)
            if (data.ContainsKey("maxhero"))
            {
                maxHeroes = int.Parse(data["maxhero"]);
            }

            // Display hidden components (space separated list)
            if (data.ContainsKey("add"))
            {
                addComponents = data["add"].Split(' ');
            }
            else
            {
                addComponents = new string[0];
            }

            // Hide components (space separated list)
            if (data.ContainsKey("remove"))
            {
                removeComponents = data["remove"].Split(' ');
            }
            else
            {
                removeComponents = new string[0];
            }

            // trigger event on condition
            if (data.ContainsKey("trigger"))
            {
                trigger = data["trigger"];
            }

            // Flags required to trigger (space separated list)
            if (data.ContainsKey("flags"))
            {
                flags = data["flags"].Split(' ');
            }
            else
            {
                flags = new string[0];
            }

            // Flags to set trigger (space separated list)
            if (data.ContainsKey("set"))
            {
                setFlags = data["set"].Split(' ');
            }
            else
            {
                setFlags = new string[0];
            }

            // Flags to clear trigger (space separated list)
            if (data.ContainsKey("clear"))
            {
                clearFlags = data["clear"].Split(' ');
            }
            else
            {
                clearFlags = new string[0];
            }
        }

        override public void ChangeReference(string oldName, string newName)
        {
            if (heroListName.Equals(oldName))
            {
                heroListName = newName;
            }
            for (int i = 0; i < nextEvent.Length; i++)
            {
                if (nextEvent[i].Equals(oldName))
                {
                    nextEvent[i] = newName;
                }
            }
            nextEvent = RemoveFromArray(nextEvent, "");

            for (int i = 0; i < failEvent.Length; i++)
            {
                if (failEvent[i].Equals(oldName))
                {
                    failEvent[i] = newName;
                }
            }
            failEvent = RemoveFromArray(failEvent, "");
            for (int i = 0; i < addComponents.Length; i++)
            {
                if (addComponents[i].Equals(oldName))
                {
                    addComponents[i] = newName;
                }
            }
            addComponents = RemoveFromArray(addComponents, "");
            for (int i = 0; i < removeComponents.Length; i++)
            {
                if (removeComponents[i].Equals(oldName))
                {
                    removeComponents[i] = newName;
                }
            }
            removeComponents = RemoveFromArray(removeComponents, "");
        }

        override public string ToString()
        {
            string nl = System.Environment.NewLine;
            string r = base.ToString();

            r += "text=\"" + originalText + "\"" + nl;

            if (!confirmText.Equals(""))
            {
                r += "confirmtext=\"" + confirmText + "\"" + nl;
            }
            if (!failText.Equals(""))
            {
                r += "failtext=\"" + failText + "\"" + nl;
            }

            if (highlight)
            {
                r += "highlight=true" + nl;
            }
            if (nextEvent.Length > 0)
            {
                r += "event=";
                foreach (string s in nextEvent)
                {
                    r += s + " ";
                }
                r = r.Substring(0, r.Length - 1) + nl;
            }
            if (failEvent.Length > 0)
            {
                r += "failevent=";
                foreach (string s in failEvent)
                {
                    r += s + " ";
                }
                r = r.Substring(0, r.Length - 1) + nl;
            }
            if (!heroListName.Equals(""))
            {
                r += "hero=" + heroListName + nl;
            }
            if (gold != 0)
            {
                r += "gold=" + gold + nl;
            }
            if (minHeroes != 0)
            {
                r += "minhero=" + minHeroes + nl;
            }
            if (maxHeroes != 0)
            {
                r += "maxhero=" + maxHeroes + nl;
            }
            if (addComponents.Length > 0)
            {
                r += "add=";
                foreach (string s in addComponents)
                {
                    r += s + " ";
                }
                r = r.Substring(0, r.Length - 1) + nl;
            }
            if (removeComponents.Length > 0)
            {
                r += "remove=";
                foreach (string s in removeComponents)
                {
                    r += s + " ";
                }
                r = r.Substring(0, r.Length - 1) + nl;
            }
            if (!trigger.Equals(""))
            {
                r += "trigger=" + trigger + nl;
            }
            if (flags.Length > 0)
            {
                r += "flags=";
                foreach (string s in flags)
                {
                    r += s + " ";
                }
                r = r.Substring(0, r.Length - 1) + nl;
            }
            if (setFlags.Length > 0)
            {
                r += "set=";
                foreach (string s in setFlags)
                {
                    r += s + " ";
                }
                r = r.Substring(0, r.Length - 1) + nl;
            }
            if (clearFlags.Length > 0)
            {
                r += "clear=";
                foreach (string s in clearFlags)
                {
                    r += s + " ";
                }
                r = r.Substring(0, r.Length - 1) + nl;
            }
            return r;
        }
    }




    // MPlaces are used to position individual monsters
    public class MPlace : QuestComponent
    {
        public bool master = false;
        new public static string type = "MPlace";
        public bool rotate = false;


        public MPlace(string s) : base(s)
        {
            locationSpecified = true;
            typeDynamic = type;
        }

        public MPlace(string name, Dictionary<string, string> data) : base(name, data)
        {
            locationSpecified = true;
            typeDynamic = type;
            master = false;
            if (data.ContainsKey("master"))
            {
                master = bool.Parse(data["master"]);
            }
            if (data.ContainsKey("rotate"))
            {
                rotate = bool.Parse(data["rotate"]);
            }
        }

        override public string ToString()
        {
            string nl = System.Environment.NewLine;
            string r = base.ToString();
            if (master)
            {
                r += "master=true" + nl;
            }
            if (rotate)
            {
                r += "rotate=true" + nl;
            }
            return r;
        }
    }

    // Super class for all quest components
    public class QuestComponent
    {
        // location on the board in squares
        public Vector2 location;
        // Has a location been speficied?
        public bool locationSpecified = false;
        // type for sub classes
        public static string type = "";
        public string typeDynamic = "";
        // name of section in ini file
        public string name;
        // image for display
        public UnityEngine.UI.Image image;

        public QuestComponent(string nameIn)
        {
            typeDynamic = type;
            name = nameIn;
            location = Vector2.zero;
        }

        // Construct from ini data
        public QuestComponent(string nameIn, Dictionary<string, string> data)
        {
            typeDynamic = type;
            name = nameIn;

            // Default to 0, 0 unless specified
            location = new Vector2(0, 0);
            locationSpecified = false;
            if (data.ContainsKey("xposition"))
            {
                locationSpecified = true;
                location.x = float.Parse(data["xposition"]);
            }

            if (data.ContainsKey("yposition"))
            {
                locationSpecified = true;
                location.y = float.Parse(data["yposition"]);
            }
        }

        public static string[] RemoveFromArray(string[] array, string element)
        {
            int count = 0;
            foreach (string s in array)
            {
                if (!s.Equals(element)) count++;
            }

            string[] trimArray = new string[count];

            int j = 0;
            for (int i = 0; i < array.Length; i++)
            {
                if (!array[i].Equals(element))
                {
                    trimArray[j++] = array[i];
                }
            }

            return trimArray;
        }

        virtual public void ChangeReference(string oldName, string newName)
        {

        }

        virtual public void RemoveReference(string refName)
        {
            ChangeReference(refName, "");
        }

        override public string ToString()
        {
            string nl = System.Environment.NewLine;
            string r = "[" + name + "]" + nl;
            if (locationSpecified)
            {
                r += "xposition=" + location.x + nl;
                r += "yposition=" + location.y + nl;
            }

            return r;
        }
    }

    public class Quest
    {
        public string name = "";
        public string description = "";
        public int minPanX;
        public int minPanY;
        public int maxPanX;
        public int maxPanY;

        public Quest(Dictionary<string, string> data)
        {
            maxPanX = 20;
            maxPanY = 20;
            minPanX = -20;
            minPanY = -20;

            if (data.ContainsKey("name"))
            {
                name = data["name"];
            }
            if (data.ContainsKey("description"))
            {
                description = data["description"];
            }

            if (data.ContainsKey("maxpanx"))
            {
                maxPanX = int.Parse(data["maxpanx"]);
            }
            if (data.ContainsKey("maxpany"))
            {
                maxPanY = int.Parse(data["maxpany"]);
            }
            if (data.ContainsKey("minpanx"))
            {
                minPanX = int.Parse(data["minpanx"]);
            }
            if (data.ContainsKey("minpany"))
            {
                minPanY = int.Parse(data["minpany"]);
            }

            CameraController.SetCameraMin(new Vector2(minPanX, minPanY));
            CameraController.SetCameraMax(new Vector2(maxPanX, maxPanY));
        }

        public void SetMaxCam(Vector2 pos)
        {
            maxPanX = Mathf.RoundToInt(pos.x);
            maxPanY = Mathf.RoundToInt(pos.y);
            CameraController.SetCameraMax(new Vector2(maxPanX, maxPanY));
        }

        public void SetMinCam(Vector2 pos)
        {
            minPanX = Mathf.RoundToInt(pos.x);
            minPanY = Mathf.RoundToInt(pos.y);
            CameraController.SetCameraMin(new Vector2(minPanX, minPanY));
        }

        override public string ToString()
        {
            string nl = System.Environment.NewLine;
            string r = "[Quest]" + nl;
            r += "name=" + name + nl;
            r += "description=\"" + description + "\"" + nl;
            if (minPanY != -20)
            {
                r += "minpany=" + minPanY + nl;
            }
            if (minPanX != -20)
            {
                r += "minpanx=" + minPanX + nl;
            }
            if (maxPanX != -20)
            {
                r += "maxpanx=" + maxPanX + nl;
            }
            if (maxPanY != -20)
            {
                r += "maxpany=" + maxPanY + nl;
            }
            return r;
        }
    }
}
