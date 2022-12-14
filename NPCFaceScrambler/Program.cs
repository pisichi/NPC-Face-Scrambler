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
        static IPatcherState<ISkyrimMod, ISkyrimModGetter> _state = null!;
        static Dictionary<string, Dictionary<string, string[]>> femaleNpcsDictionary = new Dictionary<string, Dictionary<string, string[]>>();
        static Dictionary<string, Dictionary<string, string[]>> maleNpcsDictionary = new Dictionary<string, Dictionary<string, string[]>>();



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
            _state = state;
            var outputDir = Settings.Value.FacegenOutputDirectory;

            HashSet<ModKey> PluginsToMerge = new HashSet<ModKey>();


            if (!Settings.Value.PatchFemale && !Settings.Value.PatchMale)
            {
                System.Console.WriteLine("Must at least specify one sex.");
                return;
            }

            if (Settings.Value.SourceMods.Count == 0)
            {
                System.Console.WriteLine("Must at least specify one source mod in order.");
                return;
            }


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

            System.Console.WriteLine();
            System.Console.Write("Target mod(s): ");
            foreach (var modKey in npcGroups.Select(x => x.ModKey))
            {
                System.Console.Write($"{modKey.ToString()}");
            }

            System.Console.WriteLine();
            System.Console.WriteLine();

            CreateNpcsPool();

            System.Console.WriteLine();

            Dictionary<string, Dictionary<string, string[]>> npcsDictionary;

            uint count = 0;

            uint corrupt = 0;


            foreach (var npcGroup in npcGroups)
            {

                foreach (var npc in npcGroup.Npcs)
                {

                    var npcRacename = (npc.Race.Resolve(state.LinkCache)).EditorID;
                    var npcRace = (npc.Race.Resolve(state.LinkCache)).FormKey.IDString();
                    bool isFemale = npc.Configuration.Flags.HasFlag(NpcConfiguration.Flag.Female);

                    if (isFemale)
                    {
                        npcsDictionary = femaleNpcsDictionary;
                    }
                    else
                    {
                        // continue;
                        npcsDictionary = maleNpcsDictionary;
                    }


                    // System.Console.WriteLine($"--- Patching : {npc.Name} || {count}/{npcGroup.Npcs.Count} ---");
                    System.Console.WriteLine("-------------------");

                    System.Console.WriteLine($"| Patching : {npc.FormKey.IDString()} {npc.Name} || isFemale : {isFemale} || Race : {npcRacename} {npcRace} *");


                    try
                    {
                        if (!npcsDictionary.ContainsKey(npcRace))
                        {
                            System.Console.WriteLine("|\t\t@Skipping Npc || No Race in NPC Pool");
                            corrupt++;
                            continue;
                        }

                        // check condition
                        if (!IsSelectedRace(npc.Race.Resolve(state.LinkCache)))
                        {
                            System.Console.WriteLine("|\t\t@Skipping Npc || Not in selected Race");
                            corrupt++;
                            continue;
                        }

                        // female only
                        if (!Settings.Value.PatchMale)
                        {
                            if (!isFemale)
                            {
                                System.Console.WriteLine("|\t\t@Skipping Npc || Not Female");
                                corrupt++;
                                continue;
                            }
                        }

                        // male only
                        if (!Settings.Value.PatchFemale)
                        {
                            if (isFemale)
                            {
                                System.Console.WriteLine("|\t\t@Skipping Npc || Not Male");
                                corrupt++;
                                continue;
                            }
                        }


                        string npcName = npc.Name!.ToString() ?? "test";

                        if (npcName.Contains("test"))
                        {
                            System.Console.WriteLine("|\t\t@Skipping Npc || Has unsupported Name");
                            corrupt++;
                            continue;
                        }

                        if (npcRace != null && npcsDictionary.ContainsKey(npcRace))
                        {
                            var modifiedNpc = state.PatchMod.Npcs.GetOrAddAsOverride(npc);

                            //  source Npc id
                            string originId = "";
                            // Seed if using same name
                            int seed = 0;
                            // attemp count
                            int attempt = 0;
                            bool isHasFaceGen = false;


                            // // Generate seed from name
                            if (modifiedNpc.Name != null)
                            {
                                seed = (int)modifiedNpc.Name.ToString()[0] % 32;
                            }

                            while (!isHasFaceGen)
                            {
                                attempt++;
                                System.Console.WriteLine($"|\t-Attempt : {attempt}");

                                // If reached  attempt limit, break out of the loop
                                if (attempt > 10)
                                {
                                    System.Console.WriteLine("|\t\t@Pass limit attempt - skip Npc...");
                                    corrupt++;
                                    isHasFaceGen = true;
                                    break;
                                }

                                if (!npcsDictionary[npcRace].ContainsKey(modifiedNpc.Weight.ToString()) || attempt > 3)
                                {
                                    System.Console.WriteLine("|\t\t@No weight in range or Pass limit attempt, randomizing weight...");

                                    Random rnd = new Random();

                                    var weightKeys = npcsDictionary[npcRace].Keys.ToArray();
                                    // randomize the weight from keys
                                    var randomWeight = weightKeys[rnd.Next(weightKeys.Length)];

                                    if (npcsDictionary[npcRace].ContainsKey(randomWeight.ToString()))
                                    {
                                        originId = Rand(npcsDictionary[npcRace][randomWeight.ToString()], seed);
                                    }
                                    else
                                    {
                                        System.Console.WriteLine("|\t\t@No race found");

                                        break;
                                    }
                                }
                                else
                                {
                                    System.Console.WriteLine("|\t-Pass weight check");
                                    originId = Rand(npcsDictionary[npcRace][modifiedNpc.Weight.ToString()], seed);
                                }


                                var origin = state.LoadOrder.PriorityOrder.Npc().WinningOverrides().Where(npc => (npc.FormKey.IDString().Equals(originId))).Select(npc => npc.DeepCopy()).ToArray();

                                var originNpc = origin[0];

                                System.Console.WriteLine($"|\t\t* Source NPC : {originNpc.FormKey.IDString()} {originNpc.Name} || Race : {npcRacename} || Weight : {originNpc.Weight} *");
                                System.Console.WriteLine($"|\t\t* Target NPC : {npc.FormKey.IDString()} {npc.Name} || Race : {npcRacename} || Weight : {npc.Weight} *");

                                //From NPC appreance copier
                                //HANDLE FACEGEN HERE
                                string originNifPath = state.DataFolderPath + "\\meshes\\actors\\character\\facegendata\\facegeom\\" + originNpc.FormKey.ModKey.ToString() + "\\00" + originNpc.FormKey.IDString() + ".nif";
                                string modedNifPath = outputDir + "\\meshes\\actors\\character\\facegendata\\facegeom\\" + modifiedNpc.FormKey.ModKey.ToString() + "\\00" + modifiedNpc.FormKey.IDString() + ".nif";
                                if (!File.Exists(originNifPath))
                                {
                                    Console.WriteLine("|\t\t@The following Facegen .nif does not exist. If it is within a BSA, please extract it. Patching of this NPC will be repeat.\n|\t{0}", originNifPath);
                                    // // remove the npc from the dictionary
                                    // npcsDictionary[npcRace][modifiedNpc.Weight.ToString()] = npcsDictionary[npcRace][modifiedNpc.Weight.ToString()].Where(npc => npc != originId).ToArray();
                                    // Console.WriteLine("|\t\t@removed npc {0} from {1}|{2}", originId, npcRace, modifiedNpc.Weight.ToString());
                                    // // if no more npc in weight, remove the weight from the dictionary
                                    // if (npcsDictionary[npcRace][modifiedNpc.Weight.ToString()].Length == 0)
                                    // {
                                    //     Console.WriteLine("|\t\t@removed weight {0} from {1}", modifiedNpc.Weight.ToString(), npcRace);
                                    //     npcsDictionary[npcRace].Remove(modifiedNpc.Weight.ToString());
                                    // }
                                    continue;
                                }
                                else
                                {
                                    Console.WriteLine("|\t-Found Facegen .nif. Proceeding to patch...");
                                }

                                string originDdsPath = state.DataFolderPath + "\\textures\\actors\\character\\facegendata\\facetint\\" + originNpc.FormKey.ModKey.ToString() + "\\00" + originNpc.FormKey.IDString() + ".dds";
                                string modedDdsPath = outputDir + "\\textures\\actors\\character\\facegendata\\facetint\\" + modifiedNpc.FormKey.ModKey.ToString() + "\\00" + modifiedNpc.FormKey.IDString() + ".dds";
                                if (!File.Exists(originDdsPath))
                                {
                                    Console.WriteLine("|\t\t@The following Facegen .dds does not exist. If it is within a BSA, please extract it. Patching of this NPC will be repeat.\n|\t{0}", originDdsPath);
                                    // // remove the npc from the dictionary
                                    // npcsDictionary[npcRace][modifiedNpc.Weight.ToString()] = npcsDictionary[npcRace][modifiedNpc.Weight.ToString()].Where(npc => npc != originId).ToArray();
                                    // Console.WriteLine("|\t\t@removed npc {0} from {1}|{2}", originId, npcRace, modifiedNpc.Weight.ToString());
                                    // // if no more npc in weight, remove the weight from the dictionary
                                    // if (npcsDictionary[npcRace][modifiedNpc.Weight.ToString()].Length == 0)
                                    // {
                                    //     Console.WriteLine("|\t\t@removed weight {0} from {1}", modifiedNpc.Weight.ToString(), npcRace);
                                    //     npcsDictionary[npcRace].Remove(modifiedNpc.Weight.ToString());
                                    // }
                                    continue;
                                }
                                else
                                {
                                    Console.WriteLine("|\t-Found Facegen .dds. Proceeding to patch...");
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
                                bool hasPotentialError = false;
                                string errorHp = "";
                                foreach (var hp in originNpc.HeadParts)
                                {

                                    string HeadPart = hp.Resolve(state.LinkCache).EditorID!.ToString();
                                    Console.WriteLine("|\t\t-Adding Head Part: " + HeadPart);

                                    // if (HeadPart.Contains())
                                    if (ContainsAny(HeadPart, Settings.Value.BlockHeadpart))
                                    {
                                        errorHp = HeadPart;
                                        hasPotentialError = true;
                                        Console.WriteLine("|\t\t@The following Facegen is in the block list, Skip", hp.FormKey.IDString(), HeadPart);
                                        break;
                                    }
                                    else
                                    {
                                        modifiedNpc.HeadParts.Add(hp);
                                    }
                                }

                                if (hasPotentialError)
                                {
                                    Console.WriteLine("|\t\t@The following Facegen has potential error, Skip", errorHp);
                                    continue;
                                }
                                else
                                {
                                    isHasFaceGen = true;
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



                                System.Console.WriteLine("Complete");


                                if (Settings.Value.CopyResourcesToPlugin)
                                {
                                    foreach (var FL in originNpc.ContainedFormLinks)
                                    {
                                        if (FL.FormKey.ModKey != state.PatchMod.ModKey
                                        && FL.FormKey.ModKey.ToString() != "Skyrim.esm"
                                        && FL.FormKey.ModKey.ToString() != "Dawnguard.esm"
                                        && FL.FormKey.ModKey.ToString() != "HearthFires.esm"
                                        && FL.FormKey.ModKey.ToString() != "Dragonborn.esm")
                                        {
                                            PluginsToMerge.Add(FL.FormKey.ModKey);
                                        }
                                    }
                                }



                            }
                            count++;
                        }
                        else
                        {
                            corrupt++;
                        }
                    }
                    catch (Exception e)
                    {
                        corrupt++;
                        System.Console.WriteLine($"Error : {e}");
                    }
                    finally
                    {
                        System.Console.WriteLine("-------------------\n");
                    }
                }
            }
            if (Settings.Value.CopyResourcesToPlugin)
            {
                foreach (var mk in PluginsToMerge)
                {
                    Console.WriteLine("Remapping Dependencies from {0}.", mk.ToString());
                    state.PatchMod.DuplicateFromOnlyReferenced(state.LinkCache, mk, out var _);
                }
            }
            System.Console.WriteLine($"Patches {count} Npcs");
            System.Console.WriteLine($"Skip {corrupt} Npcs");
        }

        public static void CreateNpcsPool()
        {
            var npcGroups = _state.LoadOrder.ListedOrder
               .Select(listing => listing.Mod)
               .NotNull()
               .Select(x => (x.ModKey, x.Npcs))
               .Where(x => x.Npcs.Count > 0 && Settings.Value.SourceMods.Contains(x.ModKey))
               .ToArray();

            System.Console.Write("Source Mod(s): ");
            foreach (var modKey in npcGroups.Select(x => x.ModKey))
            {
                System.Console.Write($" {modKey},");
            }

            System.Console.WriteLine();

            uint count = 0;

            Dictionary<string, Dictionary<string, string[]>> npcsDictionary;


            foreach (var npcGroup in npcGroups)
            {

                foreach (var pickedNpc in npcGroup.Npcs)
                {
                    var npcRace = (pickedNpc.Race.Resolve(_state.LinkCache)).FormKey.IDString();
                    var npcRacename = (pickedNpc.Race.Resolve(_state.LinkCache)).EditorID;
                    bool isFemale = pickedNpc.Configuration.Flags.HasFlag(NpcConfiguration.Flag.Female);
                    bool isProtected = pickedNpc.Configuration.Flags.HasFlag(NpcConfiguration.Flag.Essential) || pickedNpc.Configuration.Flags.HasFlag(NpcConfiguration.Flag.Protected);

                    try
                    {
                        if (isFemale)
                        {
                            npcsDictionary = femaleNpcsDictionary;
                        }
                        else
                        {
                            // continue;
                            npcsDictionary = maleNpcsDictionary;
                        }


                        if (npcRace == null)
                        {
                            continue;
                        }

                        // bool skip = ContainsBadHp(pickedNpc.HeadParts);
                        // if (skip)
                        // {
                        //     Console.WriteLine("|\t-Skipping NPC {0} because of bad hp", pickedNpc.Name);
                        //     break;
                        // }

                        if (!IsSelectedRace(pickedNpc.Race.Resolve(_state.LinkCache)))
                        {
                            continue;
                        }
                        if (!isProtected && Settings.Value.OnlyImportantNpc)
                        {
                            continue;
                        }
                        // add weight as key and FormKey as value to seperateWeight Dictionary, if key exists, add to array
                        if (npcsDictionary.ContainsKey(npcRace))
                        {
                            // if key weight exists, add to array
                            if (npcsDictionary[npcRace].ContainsKey(pickedNpc.Weight.ToString()))
                            {
                                npcsDictionary[npcRace][pickedNpc.Weight.ToString()] = npcsDictionary[npcRace][pickedNpc.Weight.ToString()].Concat(new string[] { pickedNpc.FormKey.IDString() }).ToArray();
                            }
                            else
                            {
                                npcsDictionary[npcRace].Add(pickedNpc.Weight.ToString(), new string[] { pickedNpc.FormKey.IDString() });
                            }
                        }
                        else
                        {
                            npcsDictionary.Add(npcRace, new Dictionary<string, string[]> { { pickedNpc.Weight.ToString(), new string[] { pickedNpc.FormKey.IDString() } } });
                        }

                        if (isFemale)
                        {
                            femaleNpcsDictionary = npcsDictionary;
                        }
                        else
                        {
                            maleNpcsDictionary = npcsDictionary;
                        }
                        count++;

                    }
                    catch (Exception e)
                    {
                        System.Console.WriteLine($"Error : {e}");
                    }
                    finally
                    {

                    }
                }
            }

            var femaleNpcsCount = 0;

            foreach (var key in femaleNpcsDictionary.Keys)
            {
                // System.Console.WriteLine("{0} : {1}", key, femaleNpcsDictionary[key]);
                foreach (var weight in femaleNpcsDictionary[key].Keys)
                {
                    // System.Console.WriteLine("\t{0} : {1}", weight, femaleNpcsDictionary[key][weight].Length);
                    femaleNpcsCount += femaleNpcsDictionary[key][weight].Length;
                    // foreach (var npc in femaleNpcsDictionary[key][weight])
                    // {
                    //     System.Console.WriteLine("\t{0}", npc);
                    // }
                }
            }
            System.Console.WriteLine();
            System.Console.WriteLine("Female npc counts: " + femaleNpcsCount);

            var maleNpcsCount = 0;

            foreach (var key in maleNpcsDictionary.Keys)
            {
                // System.Console.WriteLine("{0} : {1}", key, maleNpcsDictionary[key]);
                foreach (var weight in maleNpcsDictionary[key].Keys)
                {
                    // System.Console.WriteLine("\t{0} : {1}", weight, maleNpcsDictionary[key][weight].Length);
                    maleNpcsCount += maleNpcsDictionary[key][weight].Length;
                    // foreach (var npc in maleNpcsDictionary[key][weight])
                    // {
                    //     System.Console.WriteLine("\t{0}", npc);
                    // }
                }
            }
            System.Console.WriteLine();
            System.Console.WriteLine("Other npc counts: " + maleNpcsCount);
            System.Console.WriteLine();
            System.Console.WriteLine($"{count} Npcs add to pools");
        }

        public static bool IsSelectedRace(Mutagen.Bethesda.Skyrim.IRaceGetter race)
        {
            if (!Settings.Value.OnlyVanillaRace)
            {
                return true;
            }
            // dirty race check
            else if (
                race.Equals(Skyrim.Race.ArgonianRace)
                || race.Equals(Skyrim.Race.ArgonianRaceVampire)
                || race.Equals(Skyrim.Race.BretonRace)
                || race.Equals(Skyrim.Race.BretonRaceVampire)
                || race.Equals(Skyrim.Race.DarkElfRace)
                || race.Equals(Skyrim.Race.DarkElfRaceVampire)
                || race.Equals(Skyrim.Race.ElderRace)
                || race.Equals(Skyrim.Race.ElderRaceVampire)
                || race.Equals(Skyrim.Race.NordRace)
                || race.Equals(Skyrim.Race.NordRaceVampire)
                || race.Equals(Skyrim.Race.HighElfRace)
                || race.Equals(Skyrim.Race.HighElfRaceVampire)
                || race.Equals(Skyrim.Race.WoodElfRace)
                || race.Equals(Skyrim.Race.WoodElfRaceVampire)
                || race.Equals(Skyrim.Race.KhajiitRace)
                || race.Equals(Skyrim.Race.KhajiitRaceVampire)
                || race.Equals(Skyrim.Race.OrcRace)
                || race.Equals(Skyrim.Race.OrcRaceVampire)
                || race.Equals(Skyrim.Race.RedguardRace)
                || race.Equals(Skyrim.Race.RedguardRaceVampire)
                || race.Equals(Skyrim.Race.ImperialRace)
                || race.Equals(Skyrim.Race.ImperialRaceVampire)
            )
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool ContainsAny(string stringToTest, List<string> substrings)
        {
            if (string.IsNullOrEmpty(stringToTest) || substrings == null)
                return false;

            foreach (var substring in substrings)
            {
                if (stringToTest.Contains(substring, StringComparison.CurrentCultureIgnoreCase))
                    return true;
            }
            return false;
        }


        // public static bool ContainsBadHp(System.Collections.Generic.IReadOnlyList<Mutagen.Bethesda.IFormLinkGetter<Mutagen.Bethesda.Skyrim.IHeadPartGetter>> HeadParts)
        // {
        //     foreach (var hp in HeadParts)
        //     {
        //         string HeadPart = hp.Resolve(_state.LinkCache).EditorID!.ToString();
        //         Console.WriteLine("|\t\t-Adding Head Part: " + HeadPart);
        //         if (ContainsAny(HeadPart, Settings.Value.BlockHeadpart))
        //         {
        //             Console.WriteLine("|\t\t@The following Facegen is in the block list, Skip", HeadPart);
        //             return true;
        //         }
        //     }
        //     return false;
        // }
    }
}



