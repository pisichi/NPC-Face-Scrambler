using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using System;

namespace NPCFaceScrambler
{
    public class Program
    {
        static Lazy<Settings> Settings = null!;
        static Dictionary<string, Dictionary<string, string[]>> femaleNpcsDictionary = new Dictionary<string, Dictionary<string, string[]>>();


        public static Task<int> Main(string[] args)
        {
            return SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetAutogeneratedSettings(
                    nickname: "Settings",
                    path: "settings.json",
                    out Settings)
                .SetTypicalOpen(GameRelease.SkyrimSE, "NPCFaceScrambler.esp")
                .Run(args);
        }

        public static string Rand(string[] originId, int seed)
        {
            Random rnd;
            var isDupeName = Settings.Value.SameName;

            //Check name rule
            if (isDupeName)
            {
                rnd = new Random(seed);
            }
            else
            {
                rnd = new Random();
            }
            int index = rnd.Next(originId.Length);
            return originId[index];
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {

            var outputDir = Settings.Value.FacegenOutputDirectory;

            //From Face fixer
            if (Settings.Value.TargetMods.Count == 0)
            {
                System.Console.WriteLine("Must at least specify one target mod in order.");
                return;
            }

            var npcGroups = state.LoadOrder.ListedOrder
                .Select(listing => listing.Mod)
                .NotNull()
                .Select(x => (x.ModKey, x.Npcs))
                .Where(x => x.Npcs.Count > 0 && Settings.Value.TargetMods.Contains(x.ModKey))
                .ToArray();

            System.Console.WriteLine("Target mod:");
            foreach (var modKey in npcGroups.Select(x => x.ModKey))
            {
                System.Console.WriteLine($"{modKey}");
            }



            CreateNpcsPool(state);

            uint count = 0;


            // foreach (var npcGroup in npcGroups)
            // {
            //     System.Console.WriteLine($"{npcGroup.Npcs}");
            // }



            // // For every Npc that exists
            // foreach (var npc in state.LoadOrder.PriorityOrder.Npc().WinningOverrides())
            // {
            //For every Npc group in our target mods, in order
            foreach (var npcGroup in npcGroups)
            {

                foreach (var npc in npcGroup.Npcs)
                {

                    var modifiedNpc = state.PatchMod.Npcs.GetOrAddAsOverride(npc);
                    var npcRace = (modifiedNpc.Race.Resolve(state.LinkCache)).EditorID;
                    bool isFemale = npc.Configuration.Flags.HasFlag(NpcConfiguration.Flag.Female);

                    System.Console.WriteLine($"--- Patching : {npc.Name} || {count}/{npcGroup.Npcs.Count} ---");


                    if (npcRace != null)
                    {
                        if (!IsSelectedRace(npcRace.ToString()))
                        {
                            System.Console.WriteLine("Skipping Npc || Not in selected Race");
                            System.Console.WriteLine("--------");
                            continue;
                        }

                        if (!isFemale)
                        {
                            System.Console.WriteLine("Skipping Npc || Not Female");
                            System.Console.WriteLine("--------");
                            continue;
                        }

                        string npcName = npc.Name!.ToString() ?? "test";

                        if (npcName.Contains("test"))
                        {
                            System.Console.WriteLine("Skipping Npc || Has unsupported Name");
                            System.Console.WriteLine("--------");
                            continue;
                        }

                        // Base Source Npc id
                        string originId = "";

                        // Seed if using same name
                        int seed = 0;

                        // attemp count
                        int attempt = 0;
                        bool isHasFaceGen = false;

                        while (!isHasFaceGen)
                        {
                            attempt++;
                            System.Console.WriteLine($"///Attempt : {attempt}///");

                            // If reached  attempt limit, break out of the loop
                            if (attempt > 10)
                            {
                                System.Console.WriteLine("///Pass limit attempt - Skip the Npc///");
                                isHasFaceGen = true;
                                break;
                            }

                            if (!femaleNpcsDictionary[npcRace].ContainsKey(modifiedNpc.Weight.ToString()))
                            {
                                System.Console.WriteLine("No weight in range, randomizing weight...");

                                Random rnd = new Random();

                                var weightKeys = femaleNpcsDictionary[npcRace].Keys.ToArray();
                                // randomize the weight from keys
                                var randomWeight = weightKeys[rnd.Next(weightKeys.Length)];

                                if (femaleNpcsDictionary[npcRace].ContainsKey(randomWeight.ToString()))
                                {
                                    originId = Rand(femaleNpcsDictionary[npcRace][randomWeight.ToString()], seed);
                                }
                                else
                                {
                                    System.Console.WriteLine("No race found");
                                    System.Console.WriteLine("--------");
                                    break;
                                }
                            }
                            else
                            {
                                System.Console.WriteLine("Pass weight check");
                                originId = Rand(femaleNpcsDictionary[npcRace][modifiedNpc.Weight.ToString()], seed);
                            }

                            // Generate seed from name
                            if (modifiedNpc.Name != null)
                            {
                                seed = (int)modifiedNpc.Name.ToString()[0] % 32;
                            }


                            var origin = state.LoadOrder.PriorityOrder.Npc().WinningOverrides().Where(npc => (npc.FormKey.IDString().Equals(originId))).Select(npc => npc.DeepCopy()).ToArray();

                            var originNpc = origin[0];

                            System.Console.WriteLine("Source NPC is " + originNpc.FormKey.IDString() + " || " + originNpc.Name + " || " + "isFemale: " + isFemale + " || " + originNpc.Weight);
                            System.Console.WriteLine("Target NPC is " + modifiedNpc.FormKey.IDString() + " || " + modifiedNpc.Name);
                            System.Console.WriteLine("--------");

                            //From NPC appreance copier
                            //HANDLE FACEGEN HERE
                            string originNifPath = state.DataFolderPath + "\\meshes\\actors\\character\\facegendata\\facegeom\\" + originNpc.FormKey.ModKey.ToString() + "\\00" + originNpc.FormKey.IDString() + ".nif";
                            string modedNifPath = outputDir + "\\meshes\\actors\\character\\facegendata\\facegeom\\" + modifiedNpc.FormKey.ModKey.ToString() + "\\00" + modifiedNpc.FormKey.IDString() + ".nif";
                            if (!File.Exists(originNifPath))
                            {
                                Console.WriteLine("The following Facegen .nif does not exist. If it is within a BSA, please extract it. Patching of this NPC will be repeat.\n{0}", originNifPath);
                                continue;
                            }
                            else
                            {
                                Console.WriteLine("Found Facegen .nif. Proceeding to patch...");
                                isHasFaceGen = true;
                            }

                            string originDdsPath = state.DataFolderPath + "\\textures\\actors\\character\\facegendata\\facetint\\" + originNpc.FormKey.ModKey.ToString() + "\\00" + originNpc.FormKey.IDString() + ".dds";
                            string modedDdsPath = outputDir + "\\textures\\actors\\character\\facegendata\\facetint\\" + modifiedNpc.FormKey.ModKey.ToString() + "\\00" + modifiedNpc.FormKey.IDString() + ".dds";
                            if (!File.Exists(originDdsPath))
                            {
                                Console.WriteLine("The following Facegen .dds does not exist. If it is within a BSA, please extract it. Patching of this NPC will be repeat.\n{0}", originDdsPath);
                                continue;
                            }
                            else
                            {
                                Console.WriteLine("Found Facegen .dds. Proceeding to patch...");
                            }


                            // Copy NPC Facegen Nif and Dds from the donor to acceptor NPC

                            // first make the output paths if they don't exist
                            Directory.CreateDirectory(outputDir + "\\meshes\\actors\\character\\facegendata\\facegeom\\" + modifiedNpc.FormKey.ModKey.ToString());
                            Directory.CreateDirectory(outputDir + "\\textures\\actors\\character\\facegendata\\facetint\\" + modifiedNpc.FormKey.ModKey.ToString());

                            // then copy the facegen to those paths
                            File.Copy(originNifPath, modedNifPath, true);
                            File.Copy(originDdsPath, modedDdsPath, true);
                            // END FACEGEN

                            // //Race
                            // modifiedNpc.Race.SetTo(originNpc.Race);

                            //Head Texture
                            modifiedNpc.HeadTexture.SetTo(originNpc.HeadTexture.FormKeyNullable);

                            //Head Parts
                            modifiedNpc.HeadParts.Clear();
                            foreach (var hp in originNpc.HeadParts)
                            {
                                modifiedNpc.HeadParts.Add(hp);
                            }

                            //Face Morph
                            if (modifiedNpc.FaceMorph != null && originNpc.FaceMorph != null)
                            {
                                modifiedNpc.FaceMorph.Clear();
                                modifiedNpc.FaceMorph.DeepCopyIn(originNpc.FaceMorph);
                            }

                            //Face Parts
                            if (modifiedNpc.FaceParts != null && originNpc.FaceParts != null)
                            {
                                modifiedNpc.FaceParts.Clear();
                                modifiedNpc.FaceParts.DeepCopyIn(originNpc.FaceParts);
                            }

                            //Body Texture
                            modifiedNpc.WornArmor.SetTo(originNpc.WornArmor.FormKeyNullable);

                            //Hair Color
                            modifiedNpc.HairColor.SetTo(originNpc.HairColor.FormKeyNullable);

                            //Texture Lighting
                            modifiedNpc.TextureLighting = originNpc.TextureLighting;

                            //Tint Layers
                            modifiedNpc.TintLayers.Clear();
                            foreach (var tl in originNpc.TintLayers)
                            {
                                TintLayer newTintLayer = new TintLayer();
                                newTintLayer.DeepCopyIn(tl);
                                modifiedNpc.TintLayers.Add(newTintLayer);
                            }

                            //Height and Weight
                            modifiedNpc.Height = originNpc.Height;
                            modifiedNpc.Weight = originNpc.Weight;

                        }
                        count++;
                    }
                }
            }
            // }
            // System.Console.WriteLine("Finished" + seperateWeight);

            System.Console.WriteLine($"Patches {count} Npcs");
        }

        public static void CreateNpcsPool(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            var npcGroups = state.LoadOrder.ListedOrder
               .Select(listing => listing.Mod)
               .NotNull()
               .Select(x => (x.ModKey, x.Npcs))
               .Where(x => x.Npcs.Count > 0 && Settings.Value.SourceMods.Contains(x.ModKey))
               .ToArray();

            System.Console.WriteLine("Files to map to:");
            foreach (var modKey in npcGroups.Select(x => x.ModKey))
            {
                System.Console.WriteLine($" {modKey}");
            }

            uint count = 0;



            // For every Npc that exists
            foreach (var npcGroup in npcGroups)
            {

                foreach (var pickedNpc in npcGroup.Npcs)
                {

                    // If our target mod contains a copy of the npc
                    // if (!npcGroup.Npcs.TryGetValue(npc.FormKey, out var sourceNpc)) continue;

                    // var pickedNpc = state.PatchMod.Npcs.GetOrAddAsOverride(npc);

                    var npcRace = (pickedNpc.Race.Resolve(state.LinkCache)).EditorID;
                    bool isFemale = pickedNpc.Configuration.Flags.HasFlag(NpcConfiguration.Flag.Female);
                    bool isProtected = pickedNpc.Configuration.Flags.HasFlag(NpcConfiguration.Flag.Essential) || pickedNpc.Configuration.Flags.HasFlag(NpcConfiguration.Flag.Protected) || npcRace == "KhajiitRace" || npcRace == "DarkElfRace";
                    // bool isProtected = true;

                    // System.Console.WriteLine(pickedNpc.FormKey.IDString() + "   " + (pickedNpc.Race.Resolve(state.LinkCache)).EditorID);

                    if (npcRace != null)
                    {
                        if (!IsSelectedRace(npcRace.ToString()) || !isFemale || !isProtected)
                        {
                            continue;
                        }
                        // add weight as key and FormKey as value to seperateWeight Dictionary, if key exists, add to array
                        if (femaleNpcsDictionary.ContainsKey(npcRace))
                        {
                            // if key weight exists, add to array
                            if (femaleNpcsDictionary[npcRace].ContainsKey(pickedNpc.Weight.ToString()))
                            {
                                femaleNpcsDictionary[npcRace][pickedNpc.Weight.ToString()] = femaleNpcsDictionary[npcRace][pickedNpc.Weight.ToString()].Concat(new string[] { pickedNpc.FormKey.IDString() }).ToArray();
                            }
                            else
                            {
                                femaleNpcsDictionary[npcRace].Add(pickedNpc.Weight.ToString(), new string[] { pickedNpc.FormKey.IDString() });
                            }
                        }
                        else
                        {
                            femaleNpcsDictionary.Add(npcRace, new Dictionary<string, string[]> { { pickedNpc.Weight.ToString(), new string[] { pickedNpc.FormKey.IDString() } } });
                        }
                    }
                    count++;
                }
            }

            // loop throught each array in the dictionary and add print them to the console
            foreach (var key in femaleNpcsDictionary.Keys)
            {
                System.Console.WriteLine("{0} : {1}", key, femaleNpcsDictionary[key]);
                foreach (var weight in femaleNpcsDictionary[key].Keys)
                {
                    System.Console.WriteLine("\t\t{0} : {1}", weight, femaleNpcsDictionary[key][weight].Length);
                    // foreach (var npc in femaleNpcsDictionary[key][weight])
                    // {
                    //     System.Console.WriteLine("\t\t\t{0}", npc);
                    // }
                }
            }
            System.Console.WriteLine($"{count} Npcs add to pools");
        }

        public static bool IsSelectedRace(String race)
        {
            if (race == "RedguardRace" || race == "NordRace" || race == "BretonRace" || race == "ImperialRace" || race == "KhajiitRace" || race == "DarkElfRace" || race == "HighElfRace" || race == "WoodElfRace" || race == "ArgonianRace" || race == "OrcRace")
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}



