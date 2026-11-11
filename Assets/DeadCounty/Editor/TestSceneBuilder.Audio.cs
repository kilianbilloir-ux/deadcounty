using System;
using System.IO;
using System.Text;
using DeadCounty.Audio;
using UnityEditor;
using UnityEngine;

namespace DeadCounty.EditorTools
{
    // Sounds are synthesised here (see Synth) into 16-bit mono WAVs next to their SoundCue assets; seeds are stable
    // and unchanged files are not rewritten, so a rebuild leaves git clean. A cue with takes in Audio/Recorded plays
    // those instead: voices, animals and music, which synthesis does poorly (scripts/audio/generate_sounds.py).
    public static partial class TestSceneBuilder
    {
        const string AudioDir = "Assets/DeadCounty/Audio";
        const string RecordedDir = AudioDir + "/Recorded";
        const float StreamingThreshold = 3f;
        const float VorbisQuality = 0.7f;

        static void EnsureSounds()
        {
            EnsurePlayerSounds();
            EnsureZombieSounds();
            EnsureAmbienceSounds();
        }

        static partial void EnsurePlayerSounds();
        static partial void EnsureZombieSounds();
        static partial void EnsureAmbienceSounds();

        // name may contain one folder: "Player/Step_Grass" gives Audio/Player/Step_Grass_0.wav... and
        // Audio/Player/SC_Step_Grass.asset. render is called once per variant with an rng seeded from name and variant.
        static SoundCue CueAsset(string name, SoundChannel channel, int variants, Func<System.Random, float[]> render,
            float volume = 1f, float pitchJitter = 0.05f, float minDistance = 1f, float maxDistance = 25f)
        {
            var slash = name.LastIndexOf('/');
            var dir = slash < 0 ? AudioDir : $"{AudioDir}/{name.Substring(0, slash)}";
            var file = name.Substring(slash + 1);
            EnsureFolder(dir);

            var clips = RecordedClips(name, channel);
            if (clips.Length > 0) variants = 0;
            else clips = new AudioClip[variants];
            for (var v = 0; v < variants; v++)
                clips[v] = WriteClip($"{dir}/{file}_{v}.wav", render(new System.Random(StableSeed($"{name}#{v}"))));
            for (var v = variants; File.Exists($"{dir}/{file}_{v}.wav"); v++)
                AssetDatabase.DeleteAsset($"{dir}/{file}_{v}.wav");

            var cue = LoadOrCreate<SoundCue>($"{dir}/SC_{file}.asset");
            cue.clips = clips;
            cue.volume = volume;
            cue.pitch = new Vector2(1f - pitchJitter, 1f + pitchJitter);
            cue.channel = channel;
            cue.minDistance = minDistance;
            cue.maxDistance = maxDistance;
            EditorUtility.SetDirty(cue);
            return cue;
        }

        // Takes Recorded/<name>_0.wav, _1... when there are any. Music keeps its stereo; the rest is placed in the
        // world and plays in mono.
        static AudioClip[] RecordedClips(string name, SoundChannel channel)
        {
            var clips = new System.Collections.Generic.List<AudioClip>();
            for (var v = 0; File.Exists($"{RecordedDir}/{name}_{v}.wav"); v++)
            {
                var path = $"{RecordedDir}/{name}_{v}.wav";
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null) AssetDatabase.ImportAsset(path);
                clips.Add(ImportClip(path, AssetDatabase.LoadAssetAtPath<AudioClip>(path).length, channel != SoundChannel.Music));
            }
            return clips.ToArray();
        }

        static AudioClip WriteClip(string path, float[] samples)
        {
            var bytes = Wav(samples);
            if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
            {
                File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path);
            }
            return ImportClip(path, samples.Length / (float)Synth.Rate, true);
        }

        static AudioClip ImportClip(string path, float seconds, bool mono)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            var loadType = seconds < StreamingThreshold ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
            if (importer.forceToMono != mono || settings.loadType != loadType || settings.compressionFormat != AudioCompressionFormat.Vorbis
                || !Mathf.Approximately(settings.quality, VorbisQuality))
            {
                importer.forceToMono = mono;
                settings.loadType = loadType;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = VorbisQuality;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        static byte[] Wav(float[] samples)
        {
            var dataBytes = samples.Length * 2;
            using var stream = new MemoryStream(44 + dataBytes);
            using var w = new BinaryWriter(stream);
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataBytes);
            w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(Synth.Rate);
            w.Write(Synth.Rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(dataBytes);
            foreach (var s in samples) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * 32767f));
            w.Flush();
            return stream.ToArray();
        }

        // FNV-1a: string.GetHashCode is randomised per process.
        static int StableSeed(string text)
        {
            unchecked
            {
                var hash = 2166136261u;
                foreach (var c in text)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
