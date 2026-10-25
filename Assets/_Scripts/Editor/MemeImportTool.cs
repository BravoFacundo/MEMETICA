// Memetica > Memes > Import. Makes or updates the MemeData of every meme folder in Art/Memes from its files.
//
// Every file has its meme's 4-letter prefix, then what it is, then (optional) a number and a free suffix:
//   Reli_Base.png   Reli_Move_01.png   Reli_Move_01_Jazz.mp3   Inte_Move_06_NyanCatLoop.mp3   Mode_Fall.mp4
// Prefix → folder (full name; a new prefix makes a folder named like it):
//   Reli → Religion   Cons → Conspiracy   Inte → Internet   Mode → Mode (before: Fashion, renamed on the way)
//
// Loose files in Art/Memes (mass export from Figma or a sound site) go into their meme's folder first, keeping their
// name. A file already there for the same thing (same prefix-less role and number: Reli_Move_03.png replaces
// Move_03.png or an old move_3.png) goes to <Meme>/Old, so the new one takes its place.
//
// What each file is (any case; the number can be 1 or 01):
//   Base                 the meme as you find it (required: folders without it are skipped)
//   Fall, Leave          its Fall and Leave changes (optional)
//   Move_01, Move_02…    Move variants, in number order (the next one on each island change)
//     Images (.png, .jpg) and videos (.mp4, .webm, .mov): each of these can be a picture, a video or both
//   Hint_01… (video)     the clue videos of its paintings (none: it has no painting)
//   Reference_01… (image)  the still images of its paintings, same order as the hints
//   Sounds (.mp3, .wav, .ogg) with the same names:
//     Base         when you pick it up, and one of the meme's sounds (for changes with none of their own)
//     Move_01…     the sound of that Move
//     Fall, Leave  their sounds (Fall_02… for more, picked at random)
//     any other    the meme's sounds
//     "Loop" in the suffix (Move_06_NyanCatLoop) plays it in a loop while that change shows
//   → <Meme>/MemeData_<Meme>.asset (older ones: <Prefix>_Data, still updated if there's no MemeData_ one).
//     Each image gets its sound next to it (Base, Move_01…, Fall, Leave); one you set there by hand stays
// Subfolders (Old, Materials) are not read. Sticker pictures are set up for the shader (alpha is transparency,
// mipmaps for the border, clamped, their proportion kept, up to 2048). Any proportion works: the meme shows whole in
// the orb and in the window; 1266 x 1024 fills the window's hole exactly. Run it again after any change: it keeps what you set by hand (title,
// loops). Older names still work: original = Base, drop = Leave. The Console lists each meme and what it lacks.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

public static class MemeImportTool
{
    public const string Root = "Assets/Art/Memes";
    const string OldFolder = "Old";

    // Prefix → meme folder, and what that folder was called before
    static readonly Dictionary<string, string> Folders = new Dictionary<string, string>
    {
        { "reli", "Religion" }, { "cons", "Conspiracy" }, { "inte", "Internet" }, { "mode", "Mode" }, { "trol", "Troll" },
    };
    static readonly Dictionary<string, string> FormerFolders = new Dictionary<string, string> { { "Mode", "Fashion" } };

    enum FileKind { Image, Sound, Video, Other }

    // [prefix_]role[_number][_suffix]
    static readonly Regex FileName = new Regex(@"^(?:([a-z]{4})_)?(base|fall|leave|move|hint|reference|original|drop)(?:_0*(\d+))?(?:_(.+))?$");

    // What a file name says
    struct Parsed
    {
        public bool ok;
        public string prefix, role, suffix;
        public int number;   // 0: none
        public FileKind kind;
        public string path;
    }

    [MenuItem("Memetica/Memes/Import", false, 200)]
    public static void ImportAll()
    {
        RenameFormerFolders();
        SortLooseFiles();
        int count = 0;
        foreach (string folder in AssetDatabase.GetSubFolders(Root))
            if (ImportFolder(folder) != null) count++;
        AssetDatabase.SaveAssets();
        Debug.Log($"[Memes] Done: {count} memes imported from {Root}.");
    }

    //---- Folders and loose files

    static void RenameFormerFolders()
    {
        foreach (var pair in FormerFolders)
        {
            string now = $"{Root}/{pair.Key}", before = $"{Root}/{pair.Value}";
            if (AssetDatabase.IsValidFolder(now) || !AssetDatabase.IsValidFolder(before)) continue;
            string error = AssetDatabase.RenameAsset(before, pair.Key);
            if (string.IsNullOrEmpty(error)) Debug.Log($"[Memes] Folder {pair.Value} renamed to {pair.Key}.");
            else Debug.LogError($"[Memes] Couldn't rename {before} to {pair.Key}: {error}");
        }
    }

    static void SortLooseFiles()
    {
        int moved = 0;
        foreach (string file in Files(Root))
        {
            Parsed p = Parse(file);
            if (!p.ok || p.prefix == null || p.kind == FileKind.Other) continue;
            string folderName = FolderOf(p.prefix);
            string folder = $"{Root}/{folderName}";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Root, folderName);

            // What it replaces goes to Old
            foreach (string existing in Files(folder).Where(f => SameThing(Parse(f), p)))
            {
                if (!AssetDatabase.IsValidFolder($"{folder}/{OldFolder}")) AssetDatabase.CreateFolder(folder, OldFolder);
                string to = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{OldFolder}/{Path.GetFileName(existing)}");
                Moved(AssetDatabase.MoveAsset(existing, to), existing, to);
            }

            string target = $"{folder}/{Path.GetFileName(file)}";
            if (Moved(AssetDatabase.MoveAsset(file, target), file, target)) moved++;
        }
        if (moved > 0) Debug.Log($"[Memes] {moved} loose files sorted into their meme folders.");
    }

    // The same role, number and kind of file (the prefix and the suffix don't matter)
    static bool SameThing(Parsed a, Parsed b) => a.ok && a.kind == b.kind && a.role == b.role && a.number == b.number;

    static bool Moved(string error, string from, string to)
    {
        if (string.IsNullOrEmpty(error)) return true;
        Debug.LogError($"[Memes] Couldn't move {from} to {to}: {error}");
        return false;
    }

    static string FolderOf(string prefix) =>
        Folders.TryGetValue(prefix, out string folder) ? folder : char.ToUpper(prefix[0]) + prefix.Substring(1);

    static string PrefixOf(string folderName)
    {
        foreach (var pair in Folders) if (pair.Value == folderName) return char.ToUpper(pair.Key[0]) + pair.Key.Substring(1);
        return folderName.Length >= 4 ? folderName.Substring(0, 4) : folderName;
    }

    //---- One meme folder → its MemeData

    // The MemeData of one folder, made or updated. Null if it has no Base
    public static MemeData ImportFolder(string folder)
    {
        string folderName = Path.GetFileName(folder);
        var files = Files(folder).Select(Parse).ToList();
        var known = files.Where(f => f.ok).ToList();

        MemeImage Image(string role, int number = 0) => new MemeImage
        {
            picture = Load<Texture2D>(known.FirstOrDefault(f => f.role == role && f.number == number && f.kind == FileKind.Image).path),
            video = Load<VideoClip>(known.FirstOrDefault(f => f.role == role && f.number == number && f.kind == FileKind.Video).path),
        };

        MemeImage baseImage = Image("base");
        if (!MemeImage.Exists(baseImage)) { Debug.LogWarning($"[Memes] {folder}: no Base (picture or video), skipped.", AssetDatabase.LoadAssetAtPath<Object>(folder)); return null; }

        MemeData data = DataOf(folder, folderName);
        Undo.RecordObject(data, "Import meme");

        // Images (the sticker pictures set up for the shader; the references are paintings, left as they are)
        var stickerRoles = new[] { "base", "fall", "leave", "move" };
        foreach (Parsed f in known.Where(f => f.kind == FileKind.Image && stickerRoles.Contains(f.role))) SetUpSticker(f.path);
        var moveNumbers = known.Where(f => f.role == "move" && f.number > 0 && (f.kind == FileKind.Image || f.kind == FileKind.Video))
                               .Select(f => f.number).Distinct().OrderBy(n => n).ToList();
        MemeImage oldBase = data.baseImage, oldFall = data.fall, oldLeave = data.leave;
        List<MemeImage> oldVariants = data.variants;
        data.baseImage = baseImage;
        data.variants = moveNumbers.Select(n => Image("move", n)).ToList();
        data.fall = Image("fall");
        data.leave = Image("leave");
        // Sounds set by hand next to the images stay (a sound file found below replaces them)
        KeepSound(data.baseImage, oldBase); KeepSound(data.fall, oldFall); KeepSound(data.leave, oldLeave);
        for (int i = 0; i < data.variants.Count && oldVariants != null && i < oldVariants.Count; i++) KeepSound(data.variants[i], oldVariants[i]);
        var references = Numbered(known, "reference", FileKind.Image).Select(f => Load<Texture2D>(f.path)).ToList();
        if (references.Count > 0) data.references = references;

        // Sounds
        var sounds = known.Where(f => f.kind == FileKind.Sound).ToList();
        AudioClip baseSound = Load<AudioClip>(sounds.FirstOrDefault(f => f.role == "base").path);
        if (baseSound != null) data.baseImage.sound = baseSound;   // picked up
        foreach (int n in moveNumbers)
            SetSound(data.variants[moveNumbers.IndexOf(n)], Sound(sounds.FirstOrDefault(f => f.role == "move" && f.number == n), null));
        var rest = new List<MemeSound>();
        var fallSounds = Sounds(sounds.Where(f => f.role == "fall"), null);
        var leaveSounds = Sounds(sounds.Where(f => f.role == "leave"), null);
        if (fallSounds.Length > 0) { SetSound(data.fall, fallSounds[0]); rest.AddRange(fallSounds.Skip(1)); }
        if (leaveSounds.Length > 0) { SetSound(data.leave, leaveSounds[0]); rest.AddRange(leaveSounds.Skip(1)); }
        var others = files.Where(f => f.kind == FileKind.Sound && (!f.ok || (f.role != "move" && f.role != "fall" && f.role != "leave" && f.role != "base")));
        rest.AddRange(Sounds(others, data.fallbackSounds));
        // Fallback Sounds: the ones it had (set by hand too) plus the new ones from the files
        var fallback = new List<MemeSound>(data.fallbackSounds ?? new MemeSound[0]);
        foreach (MemeSound sound in rest)
            if (sound != null && sound.clip != null && !fallback.Exists(f => f != null && f.clip == sound.clip)) fallback.Add(sound);
        data.fallbackSounds = fallback.Where(x => x != null && x.clip != null).ToArray();

        // Painting clues (none in the folder: what it had stays)
        var hints = Numbered(known, "hint", FileKind.Video).Select(f => Load<VideoClip>(f.path)).ToList();
        if (hints.Count > 0) data.hints = hints;

        var ignored = files.Where(f => !f.ok && f.kind != FileKind.Sound && Path.GetExtension(f.path) != ".asset")
                           .Select(f => Path.GetFileName(f.path)).ToList();
        EditorUtility.SetDirty(data);
        ReportMeme(data, ignored);
        return data;
    }

    static void KeepSound(MemeImage image, MemeImage old)
    {
        if (image == null || old == null || image.sound != null) return;
        image.sound = old.sound;
        image.loop = old.loop;
    }

    static void SetSound(MemeImage image, MemeSound sound)
    {
        if (image == null || sound == null || sound.clip == null) return;
        image.loop = sound.loop || (image.sound == sound.clip && image.loop);   // (a loop set by hand for that clip stays)
        image.sound = sound.clip;
    }

    // Its MemeData: MemeData_<Folder> (the new ones), else <Prefix>_Data (the older ones, kept for the original game),
    // else the one it had under another name (renamed, keeping every reference to it). New: MemeData_<Folder>
    static MemeData DataOf(string folder, string folderName)
    {
        string path = $"{folder}/MemeData_{folderName}.asset";
        MemeData data = AssetDatabase.LoadAssetAtPath<MemeData>(path);
        if (data != null) return data;
        data = AssetDatabase.LoadAssetAtPath<MemeData>($"{folder}/{PrefixOf(folderName)}_Data.asset");
        if (data != null) return data;

        string former = Files(folder).FirstOrDefault(f => f.EndsWith(".asset") && AssetDatabase.LoadAssetAtPath<MemeData>(f) != null);
        if (former != null)
        {
            string error = AssetDatabase.RenameAsset(former, Path.GetFileNameWithoutExtension(path));
            if (!string.IsNullOrEmpty(error)) Debug.LogError($"[Memes] Couldn't rename {former}: {error}");
            return AssetDatabase.LoadAssetAtPath<MemeData>(string.IsNullOrEmpty(error) ? path : former);
        }

        data = ScriptableObject.CreateInstance<MemeData>();
        data.title = folderName;
        AssetDatabase.CreateAsset(data, path);
        return data;
    }

    static void ReportMeme(MemeData data, List<string> ignored)
    {
        var lacks = new List<string>();
        if (data.variants.Count == 0) lacks.Add("no Move_01 (no Move changes)");
        if (!data.HasFall) lacks.Add("no Fall");
        if (!data.HasLeave) lacks.Add("no Leave");
        if (data.BaseSound == null) lacks.Add("no Base sound (silent when picked up)");
        int moveSounds = data.variants.Count(v => v.sound != null);
        if (moveSounds < data.variants.Count) lacks.Add($"{data.variants.Count - moveSounds} Moves without their own sound (a fallback plays)");
        if (!data.HasPainting) lacks.Add("no Hint video (no painting: black pedestals only)");
        int videos = data.variants.Append(data.baseImage).Append(data.fall).Append(data.leave).Count(i => i != null && i.video != null);
        string text = $"[Memes] {data.title}: base + {data.variants.Count} moves" +
                      (data.HasFall ? " + fall" : "") + (data.HasLeave ? " + leave" : "") + (videos > 0 ? $" ({videos} videos)" : "") +
                      $", {data.variants.Append(data.baseImage).Append(data.fall).Append(data.leave).Count(i => i != null && i.sound != null)} own sounds + {Count(data.fallbackSounds)} fallback, " +
                      $"{data.hints.Count} hints, {data.references.Count} references.";
        if (lacks.Count > 0) text += "\n  Lacks: " + string.Join("; ", lacks);
        if (ignored.Count > 0) text += "\n  Not used (names don't follow the convention): " + string.Join(", ", ignored);
        Debug.Log(text, data);
    }

    //---- Names

    static Parsed Parse(string path)
    {
        var p = new Parsed { path = path, kind = KindOf(path) };
        Match m = FileName.Match(Path.GetFileNameWithoutExtension(path).ToLower());
        if (!m.Success) return p;
        p.ok = true;
        p.prefix = m.Groups[1].Success ? m.Groups[1].Value : null;
        p.role = m.Groups[2].Value == "original" ? "base" : m.Groups[2].Value == "drop" ? "leave" : m.Groups[2].Value;
        p.number = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
        p.suffix = m.Groups[4].Success ? m.Groups[4].Value : null;
        return p;
    }

    static List<Parsed> Numbered(List<Parsed> files, string role, FileKind kind) =>
        files.Where(f => f.role == role && f.kind == kind && f.number > 0).OrderBy(f => f.number).ToList();

    static FileKind KindOf(string path)
    {
        switch (Path.GetExtension(path).ToLower())
        {
            case ".png": case ".jpg": case ".jpeg": return FileKind.Image;
            case ".mp3": case ".wav": case ".ogg": return FileKind.Sound;
            case ".webm": case ".mp4": case ".mov": return FileKind.Video;
            default: return FileKind.Other;
        }
    }

    // The files right in a folder (no subfolders, no .meta)
    static List<string> Files(string folder) =>
        Directory.GetFiles(folder).Where(f => !f.EndsWith(".meta")).Select(f => f.Replace('\\', '/')).ToList();

    //---- Assets

    // Sticker pictures: transparency from alpha, mipmaps (the border is drawn from a blurred mip), no tiling, proportion kept
    static void SetUpSticker(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
        bool changed = importer.textureType != TextureImporterType.Default || !importer.alphaIsTransparency || !importer.mipmapEnabled
                       || importer.wrapMode != TextureWrapMode.Clamp || importer.maxTextureSize != 2048 || !importer.sRGBTexture
                       || importer.npotScale != TextureImporterNPOTScale.None;
        if (!changed) return;
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 2048;   // 1266 x 1024 (the window's hole) stays as it is
        importer.sRGBTexture = true;
        importer.npotScale = TextureImporterNPOTScale.None;   // its proportion as it is (not squeezed to 1024 x 1024)
        importer.SaveAndReimport();
    }

    static MemeSound[] Sounds(IEnumerable<Parsed> files, MemeSound[] before) =>
        files.OrderBy(f => f.path).Select(f => Sound(f, before)).ToArray();

    // Loop: "loop" in its suffix, or set by hand before for that clip
    static MemeSound Sound(Parsed file, MemeSound[] before)
    {
        AudioClip clip = Load<AudioClip>(file.path);
        MemeSound old = clip != null && before != null ? before.FirstOrDefault(s => s != null && s.clip == clip) : null;
        bool loopInName = file.suffix != null && file.suffix.Contains("loop");
        return new MemeSound { clip = clip, loop = loopInName || (old != null && old.loop) };
    }

    static int Count(MemeSound[] sounds) => sounds == null ? 0 : sounds.Count(s => s != null && s.clip != null);
    static T Load<T>(string path) where T : Object => path != null ? AssetDatabase.LoadAssetAtPath<T>(path) : null;
}
