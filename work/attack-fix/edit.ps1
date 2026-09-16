$ErrorActionPreference='Stop'
$path='GooseMergeDemoProject/Assets/Scripts/GameLogic/Runtime/Game/Demo/GooseMergeDemoBootstrap.cs'
$s=[IO.File]::ReadAllText($path).Replace("`r`n","`n")
$s=$s.Replace('            public VideoPlayer AttackVideoPlayer;', "            public Renderer AttackRenderer;`n            public float AttackSegmentStart;`n            public float AttackSourceDuration;")
$s=$s.Replace('            public bool AttackFrameReady;', '')
$s=$s.Replace('        private Camera mCamera;', "        private GooseAttackFrameCache mAttackFrames;`n        private Camera mCamera;")
$s=$s.Replace('            DestroyGeneratedSprite(ref mSquareSprite);', "            if (mAttackFrames != null) mAttackFrames.Dispose();`n            DestroyGeneratedSprite(ref mSquareSprite);")
$s=$s.Replace("            ClearEntities();`n            mGooseCount = 1;", "            ClearEntities();`n            foreach (GooseView view in mGooseViews)`n            {`n                RemoveGooseAttackEffect(view);`n                view.AttackPlaying = false;`n                view.AttackBulletFired = false;`n                view.AttackTimer = 0f;`n            }`n            mGooseCount = 1;")
$s=$s.Replace("        private void UpdateGooseAttackAnimations()`n        {", @'
        private void UpdateGooseAttackAnimations()
        {
            VideoClip clip = GooseAttackClipForWeapon();
            if (clip != null && mGooseDeathChromaKeyShader != null && (mAttackFrames == null || mAttackFrames.Clip != clip))
            {
                foreach (GooseView goose in mGooseViews) RemoveGooseAttackEffect(goose);
                if (mAttackFrames != null) mAttackFrames.Dispose();
                mAttackFrames = new GooseAttackFrameCache(transform, clip, mGooseDeathChromaKeyShader);
            }
'@)
$s=$s.Replace("                    view.AttackVideoPlayer = null;`n",'').Replace("                    view.AttackFrameReady = false;`n",'')
$start=$s.IndexOf('            float playbackSpeed = sourceDuration > 0.01f ? sourceDuration / duration : 1f;', $s.IndexOf('        private bool BeginGooseAttack'))
$end=$s.IndexOf('            view.AttackPlaying = true;', $start)
$s=$s.Substring(0,$start)+@'
            view.AttackSegmentStart = GetGooseAttackSegmentStart(view, clip, sourceDuration);
            view.AttackSourceDuration = sourceDuration;
            if (view.AttackEffectRoot == null)
            {
                view.AttackEffectRoot = GameObject.CreatePrimitive(PrimitiveType.Quad);
                view.AttackEffectRoot.name = "GooseAttack";
                view.AttackEffectRoot.transform.SetParent(view.Root.transform, false);
                Destroy(view.AttackEffectRoot.GetComponent<Collider>());
                view.AttackRenderer = view.AttackEffectRoot.GetComponent<Renderer>();
                view.AttackRenderer.enabled = false;
                view.AttackRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                view.AttackRenderer.receiveShadows = false;
            }
'@+"`n"+$s.Substring($end)
$start=$s.IndexOf('        private void RemoveGooseAttackEffect(GooseView view)')
$end=$s.IndexOf('        private float GetGooseAttackSourceDuration', $start)
$s=$s.Substring(0,$start)+@'
        private void RemoveGooseAttackEffect(GooseView view)
        {
            if (view == null) return;
            if (view.AttackRenderer != null)
            {
                view.AttackRenderer.enabled = false;
                view.AttackRenderer.sharedMaterial = null;
            }
            if (view.Renderer != null) view.Renderer.enabled = true;
        }

        private void RefreshGooseAttackVisual(GooseView view)
        {
            if (view.AttackRenderer == null) return;
            Material frame = !mGameOver && view.AttackPlaying && mAttackFrames != null
                ? mAttackFrames.GetFrame(view.AttackSegmentStart + Mathf.Clamp01(view.AttackTimer / view.AttackDuration) * view.AttackSourceDuration)
                : null;
            view.AttackRenderer.enabled = frame != null;
            view.Renderer.enabled = frame == null;
            if (frame == null) return;
            view.AttackRenderer.sharedMaterial = frame;
            view.AttackRenderer.sortingLayerID = view.Renderer.sortingLayerID;
            view.AttackRenderer.sortingOrder = view.Renderer.sortingOrder + 1;
            Vector3 scale = GetGooseAttackEffectScale(view);
            Vector3 parentScale = view.Root.transform.lossyScale;
            view.AttackEffectRoot.transform.localPosition = new Vector3(0f, 0.02f, -0.04f);
            view.AttackEffectRoot.transform.localScale = new Vector3(scale.x / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)), scale.y / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)), 1f);
        }

'@+"`n"+$s.Substring($end)
$s=$s.Replace('                UpdateGooseHealthBar(view);', "                RefreshGooseAttackVisual(view);`n                UpdateGooseHealthBar(view);")
[IO.File]::WriteAllText($path,$s.Replace("`n","`r`n"),[Text.UTF8Encoding]::new($false))
