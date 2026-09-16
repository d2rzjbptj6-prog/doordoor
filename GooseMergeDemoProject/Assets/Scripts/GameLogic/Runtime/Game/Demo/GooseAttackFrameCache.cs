using System;
using UnityEngine;
using UnityEngine.Video;

namespace Tuyoo.Game.Demo
{
    // One bounded cache for the current weapon, shared by every goose.
    internal sealed class GooseAttackFrameCache : IDisposable
    {
        private const int FrameLimit = 48;
        private readonly GameObject root;
        private readonly VideoPlayer player;
        private readonly RenderTexture decodeTarget;
        private readonly RenderTexture[] frames;
        private readonly Material[] materials;
        private readonly float length;
        private readonly double startedAt;
        private bool ready;
        private bool failed;
        private bool disposed;
        public readonly VideoClip Clip;

        public GooseAttackFrameCache(Transform parent, VideoClip clip, Shader shader)
        {
            Clip = clip;
            length = Mathf.Max(0.01f, (float)clip.length);
            startedAt = Time.realtimeSinceStartupAsDouble;
            int count = Mathf.Clamp((int)Math.Min(clip.frameCount, FrameLimit), 1, FrameLimit);
            frames = new RenderTexture[count];
            materials = new Material[count];
            for (int i = 0; i < count; i++)
            {
                materials[i] = new Material(shader);
                materials[i].SetColor("_KeyColor", Color.green);
                materials[i].SetFloat("_Threshold", 0.035f);
                materials[i].SetFloat("_Feather", 0.16f);
            }
            root = new GameObject("SharedGooseAttackDecoder");
            root.transform.SetParent(parent, false);
            decodeTarget = new RenderTexture(384, 384, 0, RenderTextureFormat.ARGB32);
            decodeTarget.Create();
            player = root.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.clip = clip;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = decodeTarget;
            player.waitForFirstFrame = true;
            player.skipOnDrop = false;
            player.sendFrameReadyEvents = true;
            player.frameReady += Capture;
            player.loopPointReached += Complete;
            player.errorReceived += Fail;
            player.Play();
        }

        private void Capture(VideoPlayer source, long frame)
        {
            if (disposed || failed || ready || frame < 0) return;
            int index = Mathf.Clamp((int)(frame * frames.Length / (double)Math.Max(1UL, Clip.frameCount)), 0, frames.Length - 1);
            if (frames[index] != null) return;
            var texture = new RenderTexture(384, 384, 0, RenderTextureFormat.ARGB32);
            texture.Create();
            Graphics.Blit(decodeTarget, texture);
            frames[index] = texture;
            materials[index].mainTexture = texture;
        }

        private void Complete(VideoPlayer source)
        {
            ready = true;
            source.Stop();
        }

        private void Fail(VideoPlayer source, string message)
        {
            failed = true;
            source.Stop();
            Debug.LogWarning("Goose attack animation unavailable: " + message);
        }

        public Material GetFrame(float time)
        {
            if (!ready && !failed && Time.realtimeSinceStartupAsDouble - startedAt > Math.Max(15f, length * 3f))
                Fail(player, "decode timed out");
            if (!ready || failed) return null;
            int index = Mathf.Clamp(Mathf.FloorToInt(time / length * frames.Length), 0, frames.Length - 1);
            // Some platforms skip frames even during preparation; use the nearest captured one.
            for (int distance = 0; distance < frames.Length; distance++)
            {
                int previous = index - distance;
                if (previous >= 0 && frames[previous] != null) return materials[previous];
                int next = index + distance;
                if (next < frames.Length && frames[next] != null) return materials[next];
            }
            return null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (player != null)
            {
                player.frameReady -= Capture;
                player.loopPointReached -= Complete;
                player.errorReceived -= Fail;
                player.Stop();
                player.targetTexture = null;
            }
            UnityEngine.Object.Destroy(root);
            decodeTarget.Release();
            UnityEngine.Object.Destroy(decodeTarget);
            for (int i = 0; i < frames.Length; i++)
            {
                UnityEngine.Object.Destroy(materials[i]);
                if (frames[i] == null) continue;
                frames[i].Release();
                UnityEngine.Object.Destroy(frames[i]);
            }
        }
    }
}
