using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Noggog;
using Mutagen.Bethesda.Synthesis.Settings;

namespace NPCFaceScrambler
{
    public record Settings
    {
        [SynthesisOrder]
        [SynthesisTooltip("Directory to which facegen should be copied (Your mod folder)")]
        public string FacegenOutputDirectory { get; set; } = "";


        [SynthesisOrder]
        [SynthesisTooltip("Source mods")]
        public List<ModKey> SourceMods = new List<ModKey>();

        [SynthesisOrder]
        [SynthesisTooltip("Target mods")]
        public List<ModKey> TargetMods = new List<ModKey>();


        [SynthesisOrder]
        [SynthesisTooltip("Use same name = same appearance")]
        public bool SameName = false;


        [SynthesisOrder]
        [SynthesisTooltip("Patch female Npcs")]
        public bool PatchFemale = true;


        [SynthesisOrder]
        [SynthesisTooltip("Patch male Npcs")]
        public bool PatchMale = true;

        [SynthesisOrder]
        [SynthesisTooltip("Copy requried records to the patch.")]
        public bool CopyResourcesToPlugin = true;

        [SynthesisOrder]
        [SynthesisTooltip("Patch only proteced and essential Npcs")]
        public bool OnlyImportantNpc = true;

        [SynthesisOrder]
        [SynthesisTooltip("Patch only Vanilla Race")]
        public bool OnlyVanillaRace = true;


        [SynthesisOrder]
        [SynthesisTooltip("Blocked head part(s)")]
        public List<String> BlockHeadpart = new List<String>();


        // [SynthesisOrder]
        // [SynthesisTooltip("Click here to select which NPCs should have their appearance transferred.")]
        // public HashSet<NACnpc> NPCs { get; set; } = new HashSet<NACnpc>();


        // [SynthesisOrder]
        // [SynthesisTooltip("NPCs source blacklist.")]
        // public IFormLinkGetter<INpcGetter> ExcludeFromSourcePool { get; set; } = FormLink<INpcGetter>.Null;

        // [SynthesisOrder]
        // [SynthesisTooltip("NPCs target blacklist.")]
        // public IFormLinkGetter<INpcGetter> ExcludeFromTarget { get; set; } = FormLink<INpcGetter>.Null;


        // [SynthesisOrder]
        // [SynthesisTooltip("NPCs source blacklist.")]
        // public IFormLinkGetter<INpcGetter> IncludeInSourcePool { get; set; } = FormLink<INpcGetter>.Null;

        // [SynthesisOrder]
        // [SynthesisTooltip("NPCs target blacklist.")]
        // public IFormLinkGetter<INpcGetter> IncludeInTarget { get; set; } = FormLink<INpcGetter>.Null;

        // [SynthesisOrder]
        // [SynthesisTooltip("Use same Weight")]
        // public bool SameWeight  = false;
    }
}
