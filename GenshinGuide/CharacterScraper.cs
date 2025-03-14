﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Tesseract;
using System.Numerics;

namespace GenshinGuide
{
    public static class CharacterScraper
    {
        private static int firstCharacterName = -1;
        private static int characterMaxLevel = 90;

        public static string ScanMainCharacterName()
        {
            int xOffset = 185;
            int yOffset = 26;
            Bitmap bm = new Bitmap(275, 30);
            Graphics g = Graphics.FromImage(bm);
            int screenLocation_X = Navigation.GetPosition().left + xOffset;
            int screenLocation_Y = Navigation.GetPosition().top + yOffset;
            g.CopyFromScreen(screenLocation_X, screenLocation_Y, 0, 0, bm.Size);

            //Image Operations
            Scraper.SetGamma(0.2, 0.2, 0.2, ref bm);
            Scraper.SetInvert(ref bm);
            Scraper.SetGrayscale(ref bm);
            Scraper.SetContrast(40.0, ref bm);
            //bm = Scraper.ResizeImage(bm, bm.Width * 2, bm.Height * 2);
            //Scraper.SetGrayscale(ref bm);
            //Scraper.SetInvert(ref bm);
            //Scraper.SetContrast(80.0, ref bm);
            UserInterface.SetNavigation_Image(bm);

            string text = Scraper.AnalyzeText(bm);
            text = text.Trim();

            if (text != "")
            {
                text = Regex.Replace(text, @"[\W_]", "");
                text = text.ToLower();
                // Get rid of space and the text right after it
                text = Regex.Replace(text, @"\s+\w*", "");
            }
            else
            {
                UserInterface.AddError(text);;
            }

            return text;
        }

        public static List<Character> ScanCharacters()
        {
            List<Character> characters = new List<Character>();

            // first character name is used to stop scanning characters
            Character character; int characterCount = 0;
            while(ScanCharacter(out character) || characterCount < 4)
            {
                if (character.IsValid())
                {
                    characters.Add(character);
                    UserInterface.IncrementCharacterCount();
                    characterCount++;
                }
                character = null;
                Navigation.SelectNextCharacter();
                UserInterface.Reset_Character();
            }


            return characters;
        }

        private static bool ScanCharacter(out Character character)
        {
            int name = -1;
            int element = -1;
            int level = -1;
            bool ascension = false;
            int experience = 0;
            int constellation = 0;
            int[] talents = new int[3];

            // Scan the Name and element of Character
            int maxRuntimes = 20;
            int currentRuntimes = 0;
            do
            {
                ScanNameAndElement(ref name, ref element);
                Navigation.SystemRandomWait(Navigation.Speed.Faster);
                currentRuntimes++;
            } while ( (name < 1 || element < 0) && (currentRuntimes < maxRuntimes) );
            
            if(name < 1 && element < 0)
            {
                UserInterface.AddError("Character Name and Element are wrong");
            }

            // Check if character has been scanned before
            if (name != firstCharacterName)
            {
                // assign the first name to serve as first index
                if (firstCharacterName == -1 && name >= 1)
                    firstCharacterName = name;

                // Scan Level and ascension
                currentRuntimes = 0;
                Navigation.SelectCharacterAttributes();
                // Used to make remove numbers altered by the stars in background
                List<int> LevelComparison = new List<int>();
                do
                {
                    level = ScanLevel(ref ascension);
                    Navigation.SystemRandomWait(Navigation.Speed.Faster);
                    currentRuntimes++;
                    // check if level exists in Level comparison
                    if (!LevelComparison.Contains(level))
                    {
                        LevelComparison.Add(level);
                        level = -1;
                    }

                } while (level == -1 && currentRuntimes < maxRuntimes);


                if (level == -1)
                {
                    UserInterface.AddError("Character Level is wrong");
                }

                // Scan Experience
                //experience = ScanExperience();
                Navigation.SystemRandomWait(Navigation.Speed.Normal);

                // Scan Constellation
                Navigation.SelectCharacterConstellation();
                constellation = ScanConstellations(name);

                // Scan Talents
                Navigation.SelectCharacterTalents();
                talents = ScanTalents(name);

                // Scale down talents due to constellations
                if(constellation >= 3)
                {
                    string[] skills;
                    if(Scraper.characterTalentConstellationOrder.TryGetValue(name,out skills))
                    {
                        // get talent if character 
                        string talent = Scraper.characterTalentConstellationOrder[name][0];
                        if (constellation >= 5)
                        {
                            talents[1] = talents[1] - 3;
                            talents[2] = talents[2] - 3;
                        }
                        else if (talent == "skill")
                        {
                            talents[1] = talents[1] - 3;
                        }
                        else
                        {
                            talents[2] = talents[2] - 3;
                        }
                    }
                    else
                    {
                        talents[1] = -1;
                        talents[2] = -1;
                    }
                }

                character = new Character(name, element, level, ascension, experience, constellation, talents);
                return true;
            }
            else
            {
                character = new Character(-1, -1, -1, ascension, experience, constellation, talents);
                return false;
            }
        }

        private static void ScanNameAndElement(ref int name, ref int element)
        {
            int xOffset = 83;
            int yOffset = 5;

            Bitmap bm = new Bitmap(220, 54);
            Graphics g = Graphics.FromImage(bm);
            int screenLocation_X = Navigation.GetPosition().left + xOffset;
            int screenLocation_Y = Navigation.GetPosition().top + yOffset;
            g.CopyFromScreen(screenLocation_X, screenLocation_Y, 0, 0, bm.Size);

            //Image Operations
            Scraper.SetGrayscale(ref bm);
            Scraper.SetInvert(ref bm);
            Scraper.SetContrast(100.0, ref bm);
            bm = Scraper.ResizeImage(bm, bm.Width * 2, bm.Height * 2);

            //string text = Scraper.AnalyzeElementAndCharName(bm);
            string text = Scraper.AnalyzeText(bm);
            text = text.ToLower();
            text = text.Trim();

            if(text != "")
            {
                text = Regex.Replace(text, @"[^\w]", "");

                // search for element in block
                string elementString = "";
                elementString = Scraper.FindElement(text);

                if(elementString != "")
                {
                    element = Scraper.GetElementalCode(elementString, true);
                    text = Regex.Replace(text, elementString, "");
                    if(text != "")
                    {
                        string characterName = text; int temp = -1;
                        // strip each char from name until found in dictionary
                        while (characterName.Length > 1)
                        {
                            temp = Scraper.GetCharacterCode(characterName, true);
                            if (temp == -1)
                            {
                                characterName = characterName.Substring(0, characterName.Length - 1);
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (characterName.Length > 1)
                        {
                            UserInterface.SetCharacter_NameAndElement(bm, characterName, elementString);
                            name = Scraper.GetCharacterCode(characterName, false);
                        }
                    }
                }
            }
        }

        private static int ScanLevel(ref bool ascension)
        {
            int level = -1;

            //int xOffset = 1035;
            int xOffset = 960;
            //int yOffset = 138;
            int yOffset = 135;
            //Bitmap bm = new Bitmap(80, 24);
            Bitmap bm = new Bitmap(165, 28);
            Graphics g = Graphics.FromImage(bm);
            int screenLocation_X = Navigation.GetPosition().left + xOffset;
            int screenLocation_Y = Navigation.GetPosition().top + yOffset;
            g.CopyFromScreen(screenLocation_X, screenLocation_Y, 0, 0, bm.Size);

            //Image Operations
            bm = Scraper.ResizeImage(bm, bm.Width * 2, bm.Height * 2);
            Scraper.SetGrayscale(ref bm);
            Scraper.SetInvert(ref bm);
            Scraper.SetContrast(30.0, ref bm);

            

            //string text = Scraper.AnalyzeFewText(bm);
            string text = Scraper.AnalyzeText(bm);
            text = text.Trim();
            text = Regex.Replace(text, @"(?![0-9/]).", "");
            text = Regex.Replace(text, @"/", " ");

            string[] temp = { text };

            if (Regex.IsMatch(text, " "))
            {
                temp = text.Split(' ');
            }
            else if (temp.Length == 1)
            {
                //int numR = (int)((temp[0].Length/2) + 1);
                //string[] temp1 = new string[2];
                //temp1[0] = temp[0].Substring(0, numR - 1);
                //temp1[1] = temp[0].Substring(temp1[0].Length + 1, numR - 1);
                //temp = temp1;
                return level;
            }

            if (temp.Length == 3)
            {
                string[] temp1 = new string[2];
                temp1[0] = temp[0];
                temp1[1] = temp[2];
                temp = temp1;
            }
            int x = -1;
            int y = -1;
            if (int.TryParse(temp[0], out x) && int.TryParse(temp[1], out y))
            {
                // level must be within 1-100
                if (x > 0 && x < 101)
                {
                    UserInterface.SetCharacter_Level(bm, x);
                    level = Convert.ToInt32(temp[0]);
                    if (level != y && level > 0 && level <= characterMaxLevel)
                    {
                        ascension = true;
                    }
                }
            }
            //else
            //{
            //    if (temp.Length > 1)
            //    {
            //        Debug.Print("Error: Found " + temp[0] + " and " + temp[1] + " instead of level");
            //        UserInterface.AddError("Found " + temp[0] + " and " + temp[1] + " instead of level");;
            //    }
            //    else
            //    {
            //        Debug.Print("Error: Found " + temp[0] + " instead of level");
            //        UserInterface.AddError("Found " + temp[0] + " instead of level");;
            //    }

            //}

            return level;
        }

        private static int ScanExperience()
        {
            int experience = 0;

            int xOffset = 1117;
            int yOffset = 151;
            Bitmap bm = new Bitmap(90, 10);
            Graphics g = Graphics.FromImage(bm);
            int screenLocation_X = Navigation.GetPosition().left + xOffset;
            int screenLocation_Y = Navigation.GetPosition().top + yOffset;
            g.CopyFromScreen(screenLocation_X, screenLocation_Y, 0, 0, bm.Size);

            //Image Operations
            bm = Scraper.ResizeImage(bm, bm.Width * 6, bm.Height * 6);
            //Scraper.SetGrayscale(ref bm);
            //Scraper.SetInvert(ref bm);
            Scraper.SetContrast(30.0, ref bm);

            string text = Scraper.AnalyzeText(bm);
            text = text.Trim();
            text = Regex.Replace(text, @"(?![0-9\s/]).", "");

            if (Regex.IsMatch(text, "/"))
            {
                string[] temp = text.Split('/');
                experience = Convert.ToInt32(temp[0]);
            }
            else
            {
                Debug.Print("Error: Found " + experience + " instead of experience");
                UserInterface.AddError("Found " + experience + " instead of experience");;
            }

            return experience;
        }

        private static int ScanConstellations(int name)
        {
            int constellation = 0;
            Color lockColor = Color.FromArgb(255,255,255,255);
            Color constellationColor = new Color();

            int xOffset = 155;
            int yOffset = 70;
            Bitmap bm = new Bitmap(1, 1);
            Graphics g = Graphics.FromImage(bm);
            int screenLocation_X = Navigation.GetPosition().left + xOffset;
            int screenLocation_Y = Navigation.GetPosition().top + yOffset;
            g.CopyFromScreen(screenLocation_X, screenLocation_Y, 0, 0, bm.Size);

            for(int i = 0; i < 6; i++)
            {
                Navigation.SystemRandomWait(Navigation.Speed.Faster);

                // Select Constellation
                Navigation.SetCursorPos(Navigation.GetPosition().left + 1130, Navigation.GetPosition().top + 180 + (i * 75));
                Navigation.sim.Mouse.LeftButtonClick();

                // Selecting the first constellation takes a while to show
                if (i == 0)
                    Navigation.SystemRandomWait(Navigation.Speed.Normal);
                else
                {
                    Navigation.SystemRandomWait(Navigation.Speed.Fast);
                }

                // Grab Color
                g.CopyFromScreen(screenLocation_X, screenLocation_Y, 0, 0, bm.Size);

                constellationColor = bm.GetPixel(0, 0);

                // Compare
                if (constellationColor == lockColor)
                {
                    // Check for character like Noelle with pure white in her constellation

                    if (name == 13 && i == 1)
                    {
                        // Check if says activate at bottom
                        Bitmap bm1 = new Bitmap(140, 24);
                        Graphics g1 = Graphics.FromImage(bm1);
                        int screenLocation_X1 = Navigation.GetPosition().left + 100;
                        int screenLocation_Y1 = Navigation.GetPosition().top + 667;
                        g1.CopyFromScreen(screenLocation_X1, screenLocation_Y1, 0, 0, bm1.Size);

                        string text = Scraper.AnalyzeText(bm1);

                        if (text == "Activate")
                        {
                            break;
                        }
                    }
                    else
                        break;
                }
                constellation = i+1;

            }

            Navigation.sim.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.ESCAPE);
            Navigation.SystemRandomWait();
            return constellation;
        }

        private static int[] ScanTalents(int name)
        {
            int[] talents = {-1,-1,-1};

            int xOffset = 165;
            int yOffset = 116;
            int monaOffset = 0;
            string text = "";
            int screenLocation_X = Navigation.GetPosition().left + xOffset;
            int screenLocation_Y = Navigation.GetPosition().top + yOffset;

            // check if character is mona or ayaka
            if(name == 19 || name == 35)
            {
                monaOffset = 1;
            }

            for(int i = 0; i < 3; i++)
            {
                Bitmap bm = new Bitmap(60, 25);
                Graphics g = Graphics.FromImage(bm);

                Navigation.SystemRandomWait(Navigation.Speed.Faster);

                Navigation.SetCursorPos(Navigation.GetPosition().left + 1130, Navigation.GetPosition().top + 110 + ((i + ((i == 2)? monaOffset: 0) ) * 60));
                Navigation.sim.Mouse.LeftButtonClick();

                // Pause for each constellation
                if (i == 0)
                {
                    Navigation.SystemRandomWait(Navigation.Speed.Normal);
                }
                else
                {
                    Navigation.SystemRandomWait(Navigation.Speed.Fast);
                }

                g.CopyFromScreen(screenLocation_X, screenLocation_Y, 0, 0, bm.Size);

                bm = Scraper.ResizeImage(bm, bm.Width * 2, bm.Height * 2);
                Scraper.SetGrayscale(ref bm);
                Scraper.SetInvert(ref bm);
                Scraper.SetContrast(60.0, ref bm);

                text = Scraper.AnalyzeText(bm);
                text = text.Trim();
                text = Regex.Replace(text, @"\D", "");
                UserInterface.SetCharacter_Talent(bm, text, i);

                int x = -1;
                if(int.TryParse(text, out x))
                {
                    if(x >= 1 && x <= 15)
                        talents[i] = x;
                }
                else
                {
                    text = Scraper.AnalyzeFewText(bm);
                    text = text.Trim();
                    text = Regex.Replace(text, @"\D", "");

                    int y = -1;
                    if (int.TryParse(text, out y))
                    {
                        if (y >= 1 && y <= 15)
                            talents[i] = y;
                    }
                    else
                    {
                        Debug.Print("Error: " + x + " is not a valid Talent Number");
                        // Try Again
                        i--;
                        UserInterface.AddError(x + " is not a valid Talent Number");
                        
                    }
                    Debug.Print("Error: " + x + " is not a valid Talent Number");
                    UserInterface.AddError(x + " is not a valid Talent Number");
                }
            }

            Navigation.sim.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.ESCAPE);
            Navigation.SystemRandomWait();

            return talents;
        }
    }
}
