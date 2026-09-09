using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Tuyoo.Game.Demo
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Tuyoo/Demo/Goose Merge Demo")]
    public sealed class GooseMergeDemoBootstrap : MonoBehaviour
    {
        private enum EntityKind
        {
            GoosePickup,
            Chicken,
            Barrel,
            Gate,
            Bullet,
        }

        private sealed class FallingEntity
        {
            public EntityKind Kind;
            public GameObject Root;
            public Transform Transform;
            public SpriteRenderer Body;
            public TextMesh Label;
            public SpriteRenderer HealthBack;
            public SpriteRenderer HealthFill;
            public float Speed;
            public int Amount;
            public int Multiplier;
            public int Health;
            public int Damage;
            public int MaxHealth;
            public float MeleeTimer;
            public bool Consumed;
        }

        private sealed class GooseSlotView
        {
            public GameObject Root;
            public SpriteRenderer Renderer;
            public int SlotIndex;
            public float ShootTimer;
        }

        private sealed class CloudView
        {
            public Transform Root;
            public float Speed;
        }

        private const float PlayerY = -3.55f;
        private const float SpawnY = 6.35f;
        private const float DespawnY = -6.95f;
        private const float PlayerMoveSpeed = 7.5f;
        private const float BaseFallSpeed = 2.6f;
        private const float BulletSpeed = 5.25f;
        private const float BulletCooldown = 0.82f;
        private const float GooseScale = 0.56f;
        private const float GooseStackLift = 0.17f;
        private const float RoadHalfWidth = 1.75f;
        private const float RoadLeft = -1.75f;
        private const float RoadRight = 1.75f;
        private const int MaxGooseSlots = 7;
        private const int SpawnBudgetTotal = 14;
        private const float EnemyMeleeY = PlayerY + 0.44f;
        private const float EnemyMeleeCooldown = 0.68f;

        private static readonly Vector3[] GooseSlotOffsets =
        {
            new Vector3(0f, 0.04f, 0f),
            new Vector3(0f, 0.78f, 0f),
            new Vector3(0.68f, 0.39f, 0f),
            new Vector3(0.68f, -0.39f, 0f),
            new Vector3(0f, -0.78f, 0f),
            new Vector3(-0.68f, -0.39f, 0f),
            new Vector3(-0.68f, 0.39f, 0f),
        };

        private static readonly float[] LaneXs =
        {
            -1.45f, -0.72f, 0f, 0.72f, 1.45f
        };

        private Camera mCamera;
        private Transform mWorldRoot;
        private Transform mBackgroundRoot;
        private Transform mEntityRoot;
        private Transform mPlayerRoot;
        private Transform mCloudRoot;
        private Canvas mHudCanvas;
        private Text mScoreText;
        private Text mCountText;
        private Text mWaveText;
        private Text mHintText;
        private Image mPlayerHealthFill;
        private Text mPlayerHealthText;
        private GameObject mGameOverPanel;
        private Text mGameOverText;
        private Text mGameOverSubText;
        private Button mRestartButton;

        private readonly List<FallingEntity> mEntities = new List<FallingEntity>();
        private readonly List<GooseSlotView> mGooseViews = new List<GooseSlotView>();
        private readonly List<CloudView> mClouds = new List<CloudView>();

        private Sprite mSquareSprite;
        private Sprite mCircleSprite;
        private Sprite mGooseSprite;
        private Sprite mChickenSprite;
        private Sprite mBarrelSprite;

        private float mTargetX;
        private int mGooseCount;
        private int mWaveIndex;
        private float mSpawnTimer;
        private float mScore;
        private int mSpawnBudget;
        private bool mGameOver;
        private bool mVictory;
        private bool mBooted;
        private float mHudPulse;

        private void Awake()
        {
            Boot();
        }

        private void OnDestroy()
        {
            CleanupGeneratedAssets();
        }

        private void Update()
        {
            if (!mBooted)
            {
                return;
            }

            HandleInput();

            if (mGameOver)
            {
                UpdateClouds();
                RefreshHud();
                return;
            }

            UpdatePlayer();
            UpdateShooting();
            UpdateEntities();
            UpdateSpawning();
            UpdateClouds();
            UpdateScore();
            RefreshHud();
        }

        private void Boot()
        {
            if (mBooted)
            {
                return;
            }

            mBooted = true;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            SetupCamera();
            SetupSprites();
            SetupWorld();
            SetupHud();
            RestartRun();
        }

        private void SetupCamera()
        {
            GameObject cameraGo = new GameObject("DemoCamera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            mCamera = cameraGo.AddComponent<Camera>();
            mCamera.orthographic = true;
            mCamera.orthographicSize = 6.5f;
            mCamera.clearFlags = CameraClearFlags.SolidColor;
            mCamera.backgroundColor = new Color(0.88f, 0.96f, 1f, 1f);
            cameraGo.AddComponent<AudioListener>();
        }

        private void SetupSprites()
        {
            mSquareSprite = CreateSprite(CreateTexture(64, 64, DrawSquareTexture), 32f);
            mCircleSprite = CreateSprite(CreateTexture(64, 64, DrawCircleTexture), 32f);
            mGooseSprite = CreateSprite(CreateTexture(96, 96, DrawGooseTexture), 32f);
            mChickenSprite = CreateSprite(CreateTexture(96, 96, DrawChickenTexture), 32f);
            mBarrelSprite = CreateSprite(CreateTexture(96, 96, DrawBarrelTexture), 32f);
        }

        private void SetupWorld()
        {
            mWorldRoot = new GameObject("DemoWorld").transform;

            mBackgroundRoot = new GameObject("Background").transform;
            mBackgroundRoot.SetParent(mWorldRoot, false);

            mEntityRoot = new GameObject("Entities").transform;
            mEntityRoot.SetParent(mWorldRoot, false);

            mPlayerRoot = new GameObject("Player").transform;
            mPlayerRoot.SetParent(mWorldRoot, false);
            mPlayerRoot.position = new Vector3(0f, PlayerY, 0f);

            mCloudRoot = new GameObject("Clouds").transform;
            mCloudRoot.SetParent(mWorldRoot, false);

            BuildBackground();
            BuildPlayerShell();
            BuildClouds();
        }

        private void SetupHud()
        {
            GameObject canvasGo = new GameObject("HUD");
            canvasGo.AddComponent<RectTransform>();
            mHudCanvas = canvasGo.AddComponent<Canvas>();
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<CanvasGroup>();
            mHudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mHudCanvas.sortingOrder = 200;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.65f;

            EnsureEventSystem();

            mScoreText = CreateHudText("ScoreText", mHudCanvas.transform, new Vector2(32f, -30f), TextAnchor.UpperLeft, 34, new Color(0.11f, 0.2f, 0.16f));
            mCountText = CreateHudText("CountText", mHudCanvas.transform, new Vector2(-32f, -30f), TextAnchor.UpperRight, 34, new Color(0.11f, 0.2f, 0.16f));
            mWaveText = CreateHudText("WaveText", mHudCanvas.transform, new Vector2(0f, -30f), TextAnchor.UpperCenter, 28, new Color(0.2f, 0.18f, 0.1f));
            mHintText = CreateHudText("HintText", mHudCanvas.transform, new Vector2(0f, 72f), TextAnchor.LowerCenter, 25, new Color(0.18f, 0.24f, 0.16f));
            CreatePlayerHealthHud();

            RectTransform hintRt = mHintText.rectTransform;
            hintRt.anchorMin = new Vector2(0.5f, 0f);
            hintRt.anchorMax = new Vector2(0.5f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);

            mGameOverPanel = new GameObject("GameOverPanel");
            mGameOverPanel.AddComponent<RectTransform>();
            mGameOverPanel.transform.SetParent(mHudCanvas.transform, false);
            Image panelImage = mGameOverPanel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.45f);
            RectTransform panelRt = mGameOverPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            mGameOverPanel.SetActive(false);

            GameObject overCard = new GameObject("CenterCard");
            overCard.AddComponent<RectTransform>();
            overCard.transform.SetParent(mGameOverPanel.transform, false);
            Image cardImage = overCard.AddComponent<Image>();
            cardImage.color = new Color(0.98f, 0.96f, 0.9f, 0.92f);
            RectTransform cardRt = overCard.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(520f, 280f);

            mGameOverText = CreateHudText("GameOverText", overCard.transform, Vector2.zero, TextAnchor.MiddleCenter, 40, new Color(0.21f, 0.16f, 0.13f));
            RectTransform overTextRt = mGameOverText.rectTransform;
            overTextRt.anchorMin = new Vector2(0.5f, 0.5f);
            overTextRt.anchorMax = new Vector2(0.5f, 0.5f);
            overTextRt.pivot = new Vector2(0.5f, 0.5f);
            overTextRt.anchoredPosition = new Vector2(0f, 38f);
            mGameOverText.text = "大鹅团灭了";

            mRestartButton = CreateButton(overCard.transform, "RestartButton", "再来一局", new Vector2(0f, -68f), new Vector2(210f, 72f), new Color(0.23f, 0.66f, 0.38f));
            mRestartButton.onClick.AddListener(RestartRun);

            mGameOverSubText = CreateHudText("GameOverSubText", overCard.transform, Vector2.zero, TextAnchor.MiddleCenter, 24, new Color(0.27f, 0.23f, 0.2f));
            RectTransform subRt = mGameOverSubText.rectTransform;
            subRt.anchorMin = new Vector2(0.5f, 0.5f);
            subRt.anchorMax = new Vector2(0.5f, 0.5f);
            subRt.pivot = new Vector2(0.5f, 0.5f);
            subRt.anchoredPosition = new Vector2(0f, -8f);
            mGameOverSubText.text = "拖动左右移动，穿过倍增门，避开鸡和木桶";
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<StandaloneInputModule>();
        }

        private void BuildBackground()
        {
            float cameraWidth = GetWorldWidth();
            float cameraHeight = GetWorldHeight();

            CreateSpriteObject("Sky", mBackgroundRoot, mSquareSprite, new Color(0.70f, 0.92f, 1f, 1f),
                new Vector3(0f, 1.15f, 0f), new Vector3(cameraWidth * 1.2f, cameraHeight * 0.75f, 1f), -120);

            CreateSpriteObject("LeftField", mBackgroundRoot, mSquareSprite, new Color(0.34f, 0.63f, 0.38f, 1f),
                new Vector3(-3.1f, -3.9f, 0f), new Vector3(3.4f, 4.8f, 1f), -112);

            CreateSpriteObject("RightField", mBackgroundRoot, mSquareSprite, new Color(0.34f, 0.63f, 0.38f, 1f),
                new Vector3(3.1f, -3.9f, 0f), new Vector3(3.4f, 4.8f, 1f), -112);

            CreateSpriteObject("Path", mBackgroundRoot, mSquareSprite, new Color(0.94f, 0.85f, 0.62f, 1f),
                new Vector3(0f, -2.78f, 0f), new Vector3(RoadHalfWidth * 2f + 0.5f, 6.2f, 1f), -100);

            CreateSpriteObject("PathShade", mBackgroundRoot, mSquareSprite, new Color(0.85f, 0.72f, 0.44f, 0.75f),
                new Vector3(0f, -2.78f, 0f), new Vector3(RoadHalfWidth * 2f - 0.8f, 5.7f, 1f), -99);

            CreateSpriteObject("Sun", mBackgroundRoot, mCircleSprite, new Color(1f, 0.9f, 0.35f, 1f),
                new Vector3(2.65f, 4.95f, 0f), new Vector3(0.92f, 0.92f, 1f), -118);

            CreateCloud(new Vector3(-2.2f, 4.95f, 0f), 0.10f, 0.95f, 0.95f);
            CreateCloud(new Vector3(0.8f, 5.35f, 0f), 0.07f, 1.18f, 0.92f);
            CreateCloud(new Vector3(2.0f, 4.45f, 0f), 0.08f, 1.02f, 0.96f);
        }

        private void BuildPlayerShell()
        {
            CreateSpriteObject("Shadow", mPlayerRoot, mCircleSprite, new Color(0f, 0f, 0f, 0.15f),
                new Vector3(0f, -0.28f, 0f), new Vector3(1.6f, 0.38f, 1f), 1);

            CreateSpriteObject("SlingshotBase", mPlayerRoot, mSquareSprite, new Color(0.56f, 0.33f, 0.18f, 1f),
                new Vector3(0.42f, 0.22f, 0f), new Vector3(0.16f, 0.62f, 1f), 9);
            CreateSpriteObject("SlingshotArmL", mPlayerRoot, mSquareSprite, new Color(0.43f, 0.24f, 0.11f, 1f),
                new Vector3(0.2f, 0.56f, 0f), new Vector3(0.10f, 0.52f, 1f), 10);
            CreateSpriteObject("SlingshotArmR", mPlayerRoot, mSquareSprite, new Color(0.43f, 0.24f, 0.11f, 1f),
                new Vector3(0.64f, 0.56f, 0f), new Vector3(0.10f, 0.52f, 1f), 10);
            CreateSpriteObject("SlingshotPouch", mPlayerRoot, mCircleSprite, new Color(0.95f, 0.73f, 0.45f, 1f),
                new Vector3(0.42f, 0.48f, 0f), new Vector3(0.18f, 0.18f, 1f), 11);
        }

        private void BuildClouds()
        {
            // Clouds are registered during BuildBackground.
        }

        private void RestartRun()
        {
            ClearEntities();

            mGooseCount = 3;
            mWaveIndex = 0;
            mSpawnTimer = 0.4f;
            mScore = 0f;
            mSpawnBudget = SpawnBudgetTotal;
            mGameOver = false;
            mVictory = false;
            mHudPulse = 0f;
            mTargetX = 0f;
            mPlayerRoot.position = new Vector3(0f, PlayerY, 0f);
            RefreshGooseFormation();
            for (int i = 0; i < mGooseViews.Count; i++)
            {
                mGooseViews[i].ShootTimer = UnityEngine.Random.Range(0.05f, 0.36f);
            }
            mGameOverPanel.SetActive(false);
            RefreshHud();
        }

        private void ClearEntities()
        {
            for (int i = 0; i < mEntities.Count; i++)
            {
                if (mEntities[i].Root != null)
                {
                    Destroy(mEntities[i].Root);
                }
            }

            mEntities.Clear();
        }

        private void UpdateScore()
        {
            if (mGameOver)
            {
                return;
            }

            mScore += Time.deltaTime * (8f + mWaveIndex * 0.35f);
            mHudPulse += Time.deltaTime;
        }

        private void UpdateShooting()
        {
            if (mGooseCount <= 0)
            {
                return;
            }

            int activeViews = Mathf.Min(mGooseViews.Count, mGooseCount);
            for (int i = 0; i < activeViews; i++)
            {
                GooseSlotView view = mGooseViews[i];
                if (view.Root == null || !view.Root.activeSelf)
                {
                    continue;
                }

                view.ShootTimer -= Time.deltaTime;
                if (view.ShootTimer > 0f)
                {
                    continue;
                }

                FireBullet(view.Root.transform.position + new Vector3(0f, 0.42f, 0f));
                float stackDelay = BulletCooldown + (i / MaxGooseSlots) * 0.08f;
                view.ShootTimer = stackDelay + UnityEngine.Random.Range(0f, 0.12f);
            }
        }

        private void FireBullet(Vector3 origin)
        {
            FallingEntity bullet = CreateEntity(EntityKind.Bullet, "Bullet_" + Time.frameCount, 2, origin.y, BulletSpeed);
            bullet.Transform.position = origin;
            bullet.Body.sprite = mCircleSprite;
            bullet.Body.color = new Color(1f, 0.76f, 0.16f, 1f);
            bullet.Body.transform.localScale = new Vector3(0.16f, 0.22f, 1f);
            bullet.Health = 1;
            bullet.Damage = 1;
            bullet.Body.sortingOrder = 30;
            mEntities.Add(bullet);
        }

        private void UpdatePlayer()
        {
            float currentX = mPlayerRoot.position.x;
            float nextX = Mathf.MoveTowards(currentX, mTargetX, PlayerMoveSpeed * Time.deltaTime);
            mPlayerRoot.position = new Vector3(nextX, PlayerY, 0f);
        }

        private void UpdateSpawning()
        {
            if (mSpawnBudget <= 0)
            {
                TryResolveVictory();
                return;
            }

            mSpawnTimer -= Time.deltaTime;
            if (mSpawnTimer > 0f)
            {
                return;
            }

            SpawnWave();
            mSpawnBudget--;
            mSpawnTimer = Mathf.Clamp(1.55f - mWaveIndex * 0.035f, 0.76f, 1.55f);
        }

        private void SpawnWave()
        {
            mWaveIndex++;

            int gateLane = UnityEngine.Random.Range(0, LaneXs.Length);
            int gateValue = ChooseGateValue();
            float speed = BaseFallSpeed + mWaveIndex * 0.08f;

            SpawnGate(gateLane, gateValue, speed, SpawnY);

            int enemyCount = mWaveIndex < 4 ? 1 : 2;
            HashSet<int> used = new HashSet<int> { gateLane };

            for (int i = 0; i < enemyCount; i++)
            {
                int lane = PickUnusedLane(used);
                used.Add(lane);
                bool barrel = (i + mWaveIndex) % 2 == 0;
                if (barrel)
                {
                    SpawnBarrel(lane, speed + 0.2f, SpawnY + 1.0f + i * 0.35f);
                }
                else
                {
                    SpawnChicken(lane, speed + 0.15f, SpawnY + 1.0f + i * 0.35f);
                }
            }

        }

        private int PickUnusedLane(HashSet<int> used)
        {
            List<int> choices = new List<int>();
            for (int i = 0; i < LaneXs.Length; i++)
            {
                if (!used.Contains(i))
                {
                    choices.Add(i);
                }
            }

            if (choices.Count == 0)
            {
                return UnityEngine.Random.Range(0, LaneXs.Length);
            }

            return choices[UnityEngine.Random.Range(0, choices.Count)];
        }

        private int ChooseGateValue()
        {
            int roll = UnityEngine.Random.Range(0, 100);
            if (roll < 45)
            {
                return 2;
            }

            if (roll < 80)
            {
                return 3;
            }

            return 4;
        }

        private void UpdateEntities()
        {
            float playerHeight = GetFormationHeight();
            float playerWidth = GetFormationWidth();
            Rect playerRect = new Rect(
                new Vector2(mPlayerRoot.position.x - playerWidth * 0.5f, mPlayerRoot.position.y - 0.06f),
                new Vector2(playerWidth, playerHeight + 0.18f));

            for (int i = mEntities.Count - 1; i >= 0; i--)
            {
                FallingEntity entity = mEntities[i];
                if (entity.Kind == EntityKind.Bullet)
                {
                    entity.Transform.position += Vector3.up * entity.Speed * Time.deltaTime;
                    TryResolveBulletHit(entity);

                    if (entity.Transform.position.y >= SpawnY + 0.6f)
                    {
                        entity.Consumed = true;
                    }
                }
                else
                {
                    if (entity.Kind == EntityKind.Chicken && entity.Transform.position.y <= EnemyMeleeY)
                    {
                        entity.Transform.position = new Vector3(entity.Transform.position.x, EnemyMeleeY, entity.Transform.position.z);
                        entity.MeleeTimer -= Time.deltaTime;
                        if (entity.MeleeTimer <= 0f)
                        {
                            DamageGoose(1);
                            entity.MeleeTimer = EnemyMeleeCooldown;
                        }
                    }
                    else
                    {
                        entity.Transform.position += Vector3.down * entity.Speed * Time.deltaTime;
                    }

                    if (!entity.Consumed && entity.Kind == EntityKind.GoosePickup && Intersects(playerRect, entity))
                    {
                        ResolvePickup(entity);
                        entity.Consumed = true;
                    }
                    else if (!entity.Consumed && entity.Kind == EntityKind.Gate && Intersects(playerRect, entity))
                    {
                        ResolveGate(entity);
                        entity.Consumed = true;
                    }
                    else if (!entity.Consumed && (entity.Kind == EntityKind.Chicken || entity.Kind == EntityKind.Barrel) && Intersects(playerRect, entity))
                    {
                        if (entity.Kind == EntityKind.Chicken)
                        {
                            entity.Transform.position = new Vector3(entity.Transform.position.x, EnemyMeleeY, entity.Transform.position.z);
                        }
                        else
                        {
                            DamageGoose(entity.Damage);
                            entity.Consumed = true;
                        }
                    }
                }

                if (entity.Transform.position.y <= DespawnY || entity.Consumed)
                {
                    Destroy(entity.Root);
                    mEntities.RemoveAt(i);
                }
            }

            TryResolveVictory();
        }

        private void ResolvePickup(FallingEntity entity)
        {
            AddGoose(entity.Amount);
            mScore += 8f * entity.Amount;
            RefreshGooseFormation();
        }

        private void ResolveGate(FallingEntity entity)
        {
            AddGoose(entity.Amount);
            mScore += 14f * entity.Amount;
            RefreshGooseFormation();
        }

        private bool TryResolveBulletHit(FallingEntity bullet)
        {
            Rect bulletRect = GetEntityRect(bullet);
            for (int i = mEntities.Count - 1; i >= 0; i--)
            {
                FallingEntity target = mEntities[i];
                if (target.Kind != EntityKind.Chicken && target.Kind != EntityKind.Barrel && target.Kind != EntityKind.Gate)
                {
                    continue;
                }

                if (!bulletRect.Overlaps(GetEntityRect(target)))
                {
                    continue;
                }

                if (target.Kind == EntityKind.Gate)
                {
                    target.Amount += Mathf.Max(1, bullet.Damage);
                    target.Multiplier = target.Amount;
                    UpdateGateVisual(target);
                    mScore += 2f;
                    bullet.Consumed = true;
                    return true;
                }

                target.Health -= Mathf.Max(1, bullet.Damage);
                UpdateEnemyHealth(target);
                if (target.Health <= 0)
                {
                    target.Consumed = true;
                    mScore += target.Kind == EntityKind.Chicken ? 6f : 4f;
                }

                bullet.Consumed = true;
                return true;
            }

            return false;
        }

        private void UpdateGateVisual(FallingEntity entity)
        {
            if (entity.Label != null)
            {
                entity.Label.text = "+" + Mathf.Max(0, entity.Amount);
            }

            float power = Mathf.Clamp01((entity.Amount - 1) / 6f);
            foreach (Transform child in entity.Root.transform)
            {
                if (child.name == "Beam")
                {
                    var sr = child.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.color = Color.Lerp(new Color(0.62f, 0.97f, 0.78f, 0.24f), new Color(0.3f, 1f, 0.95f, 0.4f), power);
                    }
                }
                else if (child.name == "Glow")
                {
                    var sr = child.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.color = Color.Lerp(new Color(0.72f, 1f, 0.9f, 0.16f), new Color(0.85f, 1f, 0.6f, 0.24f), power);
                    }
                }
            }
        }

        private void TryResolveVictory()
        {
            if (mGameOver || mVictory || mSpawnBudget > 0)
            {
                return;
            }

            for (int i = 0; i < mEntities.Count; i++)
            {
                EntityKind kind = mEntities[i].Kind;
                if (kind == EntityKind.Chicken || kind == EntityKind.Barrel || kind == EntityKind.Gate)
                {
                    return;
                }
            }

            mVictory = true;
            SetGameOver(true);
        }

        private void UpdateEnemyHealth(FallingEntity entity)
        {
            if (entity.HealthBack == null || entity.HealthFill == null)
            {
                return;
            }

            float percent = entity.MaxHealth <= 0 ? 0f : Mathf.Clamp01(entity.Health / (float)entity.MaxHealth);
            entity.HealthFill.transform.localScale = new Vector3(0.62f * percent, 0.09f, 1f);
            entity.HealthFill.color = Color.Lerp(new Color(0.92f, 0.18f, 0.18f, 1f), new Color(0.14f, 0.78f, 0.34f, 1f), percent);
            entity.HealthBack.gameObject.SetActive(percent > 0f);
        }

        private void AddGoose(int amount)
        {
            if (mGameOver)
            {
                return;
            }

            mGooseCount += Mathf.Max(0, amount);
        }

        private void DamageGoose(int amount)
        {
            if (mGameOver)
            {
                return;
            }

            mGooseCount -= Mathf.Max(1, amount);
            if (mGooseCount <= 0)
            {
                mGooseCount = 0;
                SetGameOver(true);
            }
        }

        private void SetGameOver(bool gameOver)
        {
            mGameOver = gameOver;
            mGameOverPanel.SetActive(gameOver);
        }

        private void RefreshGooseFormation()
        {
            int desired = Mathf.Max(0, mGooseCount);
            while (mGooseViews.Count < desired)
            {
                mGooseViews.Add(CreateGooseView(mGooseViews.Count));
            }

            for (int i = 0; i < mGooseViews.Count; i++)
            {
                bool active = i < desired;
                GooseSlotView view = mGooseViews[i];
                view.Root.SetActive(active);
                if (!active)
                {
                    continue;
                }

                int slotIndex = i % MaxGooseSlots;
                int stackIndex = i / MaxGooseSlots;
                Vector3 slot = GooseSlotOffsets[slotIndex];
                view.SlotIndex = slotIndex;
                view.Root.transform.localPosition = slot + new Vector3(0f, stackIndex * GooseStackLift, 0f);
                view.Root.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
                view.Renderer.sortingOrder = 20 + slotIndex * 5 + stackIndex;

                float bob = Mathf.Sin(Time.time * 5f + i * 0.35f) * 0.025f;
                view.Root.transform.localScale = Vector3.one * (GooseScale + bob);
            }

            mPlayerRoot.position = new Vector3(Mathf.Clamp(mPlayerRoot.position.x, GetLeftBound(), GetRightBound()), PlayerY, 0f);
            mTargetX = Mathf.Clamp(mTargetX, GetLeftBound(), GetRightBound());
        }

        private GooseSlotView CreateGooseView(int index)
        {
            GameObject go = new GameObject("Goose_" + index);
            go.transform.SetParent(mPlayerRoot, false);
            go.transform.localScale = Vector3.one * GooseScale;

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = mGooseSprite;
            renderer.color = new Color(1f, 1f, 1f, 1f);
            renderer.sortingLayerName = "Default";

            return new GooseSlotView
            {
                Root = go,
                Renderer = renderer,
                SlotIndex = index % MaxGooseSlots,
                ShootTimer = UnityEngine.Random.Range(0.05f, 0.36f),
            };
        }

        private void SpawnGate(int lane, int gateValue, float speed, float y)
        {
            FallingEntity entity = CreateEntity(EntityKind.Gate, "Gate_" + mWaveIndex, lane, y, speed);
            entity.Multiplier = gateValue;
            entity.Amount = gateValue;

            entity.Body.enabled = false;

            CreateGateVisual(entity);
            UpdateGateVisual(entity);

            mEntities.Add(entity);
        }

        private void SpawnChicken(int lane, float speed, float y)
        {
            FallingEntity entity = CreateEntity(EntityKind.Chicken, "Chicken_" + mWaveIndex, lane, y, speed);
            entity.Amount = 1;
            entity.Health = 2;
            entity.MaxHealth = 2;
            entity.Damage = 1;
            entity.Body.sprite = mChickenSprite;
            entity.Body.color = Color.white;
            entity.Body.transform.localScale = new Vector3(1.0f, 1.0f, 1f);
            entity.Transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            CreateEnemyHealthBar(entity, new Color(0.91f, 0.2f, 0.2f, 1f));
            mEntities.Add(entity);
        }

        private void SpawnBarrel(int lane, float speed, float y)
        {
            FallingEntity entity = CreateEntity(EntityKind.Barrel, "Barrel_" + mWaveIndex, lane, y, speed);
            entity.Amount = 2;
            entity.Health = 1;
            entity.MaxHealth = 1;
            entity.Damage = 1;
            entity.Body.sprite = mBarrelSprite;
            entity.Body.color = Color.white;
            entity.Body.transform.localScale = new Vector3(0.92f, 0.92f, 1f);
            entity.Transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            CreateEnemyHealthBar(entity, new Color(0.91f, 0.58f, 0.17f, 1f));
            mEntities.Add(entity);
        }

        private void SpawnGoosePickup(int lane, float speed, float y)
        {
            FallingEntity entity = CreateEntity(EntityKind.GoosePickup, "BonusGoose_" + mWaveIndex, lane, y, speed);
            entity.Amount = 1;
            entity.Body.sprite = mGooseSprite;
            entity.Body.color = new Color(1f, 1f, 1f, 0.96f);
            entity.Body.transform.localScale = new Vector3(0.82f, 0.82f, 1f);
            entity.Transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            entity.Label = CreateWorldLabel(entity.Root.transform, "+1", new Vector3(0.02f, 0.62f, 0f), 0.14f, new Color(0.12f, 0.32f, 0.15f));
            mEntities.Add(entity);
        }

        private FallingEntity CreateEntity(EntityKind kind, string name, int lane, float y, float speed)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(mEntityRoot, false);
            root.transform.position = new Vector3(LaneXs[Mathf.Clamp(lane, 0, LaneXs.Length - 1)], y, 0f);

            SpriteRenderer body = root.AddComponent<SpriteRenderer>();
            body.sprite = mSquareSprite;
            body.color = Color.white;
            body.sortingOrder = kind == EntityKind.Gate ? 8 : 12;

            return new FallingEntity
            {
                Kind = kind,
                Root = root,
                Transform = root.transform,
                Body = body,
                Speed = speed,
                Amount = 1,
                Multiplier = 1,
                Health = 1,
                Damage = 1,
                MaxHealth = 1,
            };
        }

        private void CreateGateVisual(FallingEntity entity)
        {
            Transform parent = entity.Root.transform;
            GameObject left = new GameObject("LeftPost");
            left.transform.SetParent(parent, false);
            SpriteRenderer leftSr = left.AddComponent<SpriteRenderer>();
            leftSr.sprite = mSquareSprite;
            leftSr.color = new Color(0.22f, 0.82f, 0.53f, 0.92f);
            leftSr.sortingOrder = 9;
            left.transform.localPosition = new Vector3(-0.34f, -0.12f, 0f);
            left.transform.localScale = new Vector3(0.22f, 1.4f, 1f);

            GameObject right = new GameObject("RightPost");
            right.transform.SetParent(parent, false);
            SpriteRenderer rightSr = right.AddComponent<SpriteRenderer>();
            rightSr.sprite = mSquareSprite;
            rightSr.color = new Color(0.22f, 0.82f, 0.53f, 0.92f);
            rightSr.sortingOrder = 9;
            right.transform.localPosition = new Vector3(0.34f, -0.12f, 0f);
            right.transform.localScale = new Vector3(0.22f, 1.4f, 1f);

            GameObject beam = new GameObject("Beam");
            beam.transform.SetParent(parent, false);
            SpriteRenderer beamSr = beam.AddComponent<SpriteRenderer>();
            beamSr.sprite = mSquareSprite;
            beamSr.color = new Color(0.62f, 0.97f, 0.78f, 0.24f);
            beamSr.sortingOrder = 8;
            beam.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            beam.transform.localScale = new Vector3(0.94f, 1.62f, 1f);

            GameObject glow = new GameObject("Glow");
            glow.transform.SetParent(parent, false);
            SpriteRenderer glowSr = glow.AddComponent<SpriteRenderer>();
            glowSr.sprite = mCircleSprite;
            glowSr.color = new Color(0.72f, 1f, 0.9f, 0.16f);
            glowSr.sortingOrder = 7;
            glow.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            glow.transform.localScale = new Vector3(1.2f, 1.92f, 1f);

            entity.Label = CreateWorldLabel(parent, "+0", new Vector3(0f, 0.58f, 0f), 0.17f, new Color(0.12f, 0.24f, 0.17f));
        }

        private void CreateEnemyHealthBar(FallingEntity entity, Color fillColor)
        {
            GameObject back = new GameObject("HealthBack");
            back.transform.SetParent(entity.Root.transform, false);
            back.transform.localPosition = new Vector3(0f, 0.82f, 0f);
            back.transform.localScale = new Vector3(0.62f, 0.09f, 1f);

            SpriteRenderer backSr = back.AddComponent<SpriteRenderer>();
            backSr.sprite = mSquareSprite;
            backSr.color = new Color(0.08f, 0.08f, 0.08f, 0.65f);
            backSr.sortingOrder = 25;

            GameObject fill = new GameObject("HealthFill");
            fill.transform.SetParent(entity.Root.transform, false);
            fill.transform.localPosition = new Vector3(0f, 0.82f, 0f);
            fill.transform.localScale = new Vector3(0.62f, 0.09f, 1f);

            SpriteRenderer fillSr = fill.AddComponent<SpriteRenderer>();
            fillSr.sprite = mSquareSprite;
            fillSr.color = fillColor;
            fillSr.sortingOrder = 26;

            entity.HealthBack = backSr;
            entity.HealthFill = fillSr;
            UpdateEnemyHealth(entity);
        }

        private bool Intersects(Rect playerRect, FallingEntity entity)
        {
            Rect entityRect = GetEntityRect(entity);
            return playerRect.Overlaps(entityRect);
        }

        private Rect GetEntityRect(FallingEntity entity)
        {
            switch (entity.Kind)
            {
                case EntityKind.Gate:
                    return new Rect(entity.Transform.position.x - 0.48f, entity.Transform.position.y - 0.92f, 0.96f, 1.85f);
                case EntityKind.Chicken:
                    return new Rect(entity.Transform.position.x - 0.28f, entity.Transform.position.y - 0.28f, 0.56f, 0.56f);
                case EntityKind.Barrel:
                    return new Rect(entity.Transform.position.x - 0.28f, entity.Transform.position.y - 0.32f, 0.56f, 0.64f);
                case EntityKind.GoosePickup:
                    return new Rect(entity.Transform.position.x - 0.24f, entity.Transform.position.y - 0.24f, 0.48f, 0.48f);
                case EntityKind.Bullet:
                    return new Rect(entity.Transform.position.x - 0.12f, entity.Transform.position.y - 0.14f, 0.24f, 0.28f);
                default:
                    return new Rect(entity.Transform.position.x - 0.4f, entity.Transform.position.y - 0.4f, 0.8f, 0.8f);
            }
        }

        private void HandleInput()
        {
            float pointerWorldX;
            if (TryReadPointerWorldX(out pointerWorldX))
            {
                mTargetX = Mathf.Clamp(pointerWorldX, GetLeftBound(), GetRightBound());
            }

            float axis = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(axis) > 0.1f)
            {
                mTargetX += axis * PlayerMoveSpeed * Time.deltaTime;
                mTargetX = Mathf.Clamp(mTargetX, GetLeftBound(), GetRightBound());
            }
        }

        private bool TryReadPointerWorldX(out float worldX)
        {
            Vector2 screen;
            bool active = false;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                active = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                screen = touch.position;
            }
            else if (Input.GetMouseButton(0))
            {
                active = true;
                screen = Input.mousePosition;
            }
            else
            {
                worldX = 0f;
                return false;
            }

            if (!active)
            {
                worldX = 0f;
                return false;
            }

            Vector3 world = mCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -mCamera.transform.position.z));
            worldX = world.x;
            return true;
        }

        private void RefreshHud()
        {
            int stacks = Mathf.Min(MaxGooseSlots, Mathf.Max(1, mGooseCount));
            mScoreText.text = "得分  " + Mathf.FloorToInt(mScore).ToString();
            mCountText.text = "大鹅  " + mGooseCount.ToString();
            mWaveText.text = "门关 " + mWaveIndex.ToString() + "  |  鹅摞 " + stacks.ToString() + "/7";
            RefreshPlayerHealthHud();

            if (!mGameOver)
            {
                string tip = mWaveIndex < 2
                    ? "左右移动穿过倍增门，子弹打门会让 + 数继续涨"
                    : "每只鹅都会射击，鸡冲到身前会近战吃掉大鹅";
                mHintText.text = tip;
            }
            else
            {
                mHintText.text = mVictory ? "点中间按钮重开，再刷一轮门关" : "点中间按钮重开，继续堆大鹅";
            }

            if (mGameOver)
            {
                mGameOverText.text = mVictory ? "清场胜利！" : "大鹅团灭了";
                if (mGameOverSubText != null)
                {
                    mGameOverSubText.text = mVictory ? "所有鸡、木桶和倍增门都已清除" : "鸡冲到身前会一刀带走一只大鹅";
                }
            }
        }

        private void RefreshPlayerHealthHud()
        {
            if (mPlayerHealthFill == null || mPlayerHealthText == null)
            {
                return;
            }

            float percent = Mathf.Clamp01(mGooseCount / 20f);
            RectTransform rt = mPlayerHealthFill.rectTransform;
            rt.sizeDelta = new Vector2(232f * Mathf.Max(0.12f, percent), 36f);
            mPlayerHealthFill.color = Color.Lerp(new Color(0.92f, 0.27f, 0.25f, 1f), new Color(0.25f, 0.74f, 0.35f, 1f), percent);
            mPlayerHealthText.text = "大鹅血条  " + mGooseCount.ToString();
        }

        private void UpdateClouds()
        {
            for (int i = 0; i < mClouds.Count; i++)
            {
                CloudView cloud = mClouds[i];
                if (cloud.Root == null)
                {
                    continue;
                }

                cloud.Root.position += Vector3.left * cloud.Speed * Time.deltaTime;
                if (cloud.Root.position.x < -4.8f)
                {
                    cloud.Root.position = new Vector3(4.9f, cloud.Root.position.y, cloud.Root.position.z);
                }
            }
        }

        private void CreateCloud(Vector3 position, float speed, float width, float alpha)
        {
            GameObject cloudRoot = new GameObject("Cloud");
            cloudRoot.transform.SetParent(mCloudRoot, false);
            cloudRoot.transform.position = position;

            float baseAlpha = Mathf.Clamp01(alpha);
            CreateSpriteObject("BlobA", cloudRoot.transform, mCircleSprite, new Color(1f, 1f, 1f, baseAlpha),
                new Vector3(-0.22f, 0f, 0f), new Vector3(width * 0.42f, width * 0.34f, 1f), -115);
            CreateSpriteObject("BlobB", cloudRoot.transform, mCircleSprite, new Color(1f, 1f, 1f, baseAlpha * 0.95f),
                new Vector3(0f, 0.05f, 0f), new Vector3(width * 0.52f, width * 0.42f, 1f), -114);
            CreateSpriteObject("BlobC", cloudRoot.transform, mCircleSprite, new Color(1f, 1f, 1f, baseAlpha * 0.92f),
                new Vector3(0.24f, 0f, 0f), new Vector3(width * 0.38f, width * 0.30f, 1f), -115);

            mClouds.Add(new CloudView
            {
                Root = cloudRoot.transform,
                Speed = speed,
            });
        }

        private void UpdateGooseShadow()
        {
            // reserved for future polish
        }

        private float GetFormationWidth()
        {
            return 2.15f;
        }

        private float GetFormationHeight()
        {
            int stackLayers = Mathf.Max(1, Mathf.CeilToInt(mGooseCount / (float)MaxGooseSlots));
            return 1.85f + (stackLayers - 1) * GooseStackLift;
        }

        private float GetLeftBound()
        {
            return RoadLeft + 0.35f;
        }

        private float GetRightBound()
        {
            return RoadRight - 0.35f;
        }

        private float GetWorldWidth()
        {
            return mCamera.orthographicSize * 2f * mCamera.aspect;
        }

        private float GetWorldHeight()
        {
            return mCamera.orthographicSize * 2f;
        }

        private void CreateSpriteObject(string name, Transform parent, Sprite sprite, Color color, Vector3 localPos, Vector3 localScale, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
        }

        private Text CreateHudText(string name, Transform parent, Vector2 anchoredPosition, TextAnchor anchor, int size, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = new Vector2(640f, 80f);

            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = "";
            return text;
        }

        private void CreatePlayerHealthHud()
        {
            GameObject panel = new GameObject("PlayerHealthPanel");
            panel.transform.SetParent(mHudCanvas.transform, false);

            RectTransform panelRt = panel.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 1f);
            panelRt.anchorMax = new Vector2(0f, 1f);
            panelRt.pivot = new Vector2(0f, 1f);
            panelRt.anchoredPosition = new Vector2(32f, -80f);
            panelRt.sizeDelta = new Vector2(240f, 40f);

            Image back = panel.AddComponent<Image>();
            back.sprite = mSquareSprite;
            back.type = Image.Type.Simple;
            back.color = new Color(0.15f, 0.12f, 0.11f, 0.78f);

            GameObject fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(panel.transform, false);
            RectTransform fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = new Vector2(2f, 0f);
            fillRt.sizeDelta = new Vector2(236f, 36f);

            mPlayerHealthFill = fillGo.AddComponent<Image>();
            mPlayerHealthFill.sprite = mSquareSprite;
            mPlayerHealthFill.type = Image.Type.Simple;
            mPlayerHealthFill.color = new Color(0.93f, 0.3f, 0.28f, 1f);

            mPlayerHealthText = CreateHudText("PlayerHealthText", panel.transform, new Vector2(10f, -2f), TextAnchor.MiddleLeft, 20, Color.white);
            RectTransform textRt = mPlayerHealthText.rectTransform;
            textRt.anchorMin = new Vector2(0f, 0.5f);
            textRt.anchorMax = new Vector2(1f, 0.5f);
            textRt.offsetMin = new Vector2(10f, -14f);
            textRt.offsetMax = new Vector2(-10f, 14f);
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;

            Image image = go.AddComponent<Image>();
            image.sprite = mSquareSprite;
            image.type = Image.Type.Simple;
            image.color = color;

            Button button = go.AddComponent<Button>();

            Text text = CreateHudText("LabelText", go.transform, Vector2.zero, TextAnchor.MiddleCenter, 28, Color.white);
            RectTransform textRt = text.rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            text.text = label;

            return button;
        }

        private TextMesh CreateWorldLabel(Transform parent, string text, Vector3 localPosition, float size, Color color)
        {
            GameObject go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            TextMesh mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.fontSize = 48;
            mesh.characterSize = size;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sortingOrder = 90;
            return mesh;
        }

        private Sprite CreateSprite(Texture2D texture, float pixelsPerUnit)
        {
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        private Texture2D CreateTexture(int width, int height, Action<Texture2D> drawer)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            drawer(texture);
            return texture;
        }

        private void DrawSquareTexture(Texture2D texture)
        {
            FillTexture(texture, new Color(1f, 1f, 1f, 1f));
        }

        private void DrawCircleTexture(Texture2D texture)
        {
            FillTexture(texture, new Color(0f, 0f, 0f, 0f));

            float cx = (texture.width - 1) * 0.5f;
            float cy = (texture.height - 1) * 0.5f;
            float rx = texture.width * 0.5f - 1f;
            float ry = texture.height * 0.5f - 1f;

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    float dx = (x - cx) / rx;
                    float dy = (y - cy) / ry;
                    float d = dx * dx + dy * dy;
                    if (d <= 1f)
                    {
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, 1f));
                    }
                    else if (d <= 1.05f)
                    {
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1.05f - d) * 20f));
                    }
                }
            }
        }

        private void DrawGooseTexture(Texture2D texture)
        {
            FillTexture(texture, new Color(0f, 0f, 0f, 0f));

            DrawEllipse(texture, 49f, 52f, 20f, 16f, new Color(0.97f, 0.98f, 0.99f, 1f));
            DrawEllipse(texture, 31f, 34f, 14f, 12f, new Color(0.97f, 0.98f, 0.99f, 1f));
            DrawEllipse(texture, 43f, 41f, 14f, 19f, new Color(0.99f, 0.99f, 1f, 1f));
            DrawEllipse(texture, 34f, 52f, 6f, 9f, new Color(0.93f, 0.94f, 0.96f, 1f));
            DrawTriangle(texture, new Vector2(17f, 33f), new Vector2(31f, 37f), new Vector2(22f, 26f), new Color(1f, 0.68f, 0.16f, 1f));
            DrawEllipse(texture, 28f, 36f, 2.2f, 2.2f, Color.black);
            DrawRect(texture, 35f, 13f, 8f, 8f, new Color(1f, 0.68f, 0.16f, 1f));
            DrawRect(texture, 45f, 13f, 8f, 8f, new Color(1f, 0.68f, 0.16f, 1f));
            DrawTriangle(texture, new Vector2(49f, 44f), new Vector2(62f, 40f), new Vector2(51f, 35f), new Color(0.78f, 0.84f, 0.88f, 1f));
        }

        private void DrawChickenTexture(Texture2D texture)
        {
            FillTexture(texture, new Color(0f, 0f, 0f, 0f));

            DrawEllipse(texture, 44f, 44f, 20f, 16f, new Color(0.99f, 0.88f, 0.32f, 1f));
            DrawEllipse(texture, 59f, 56f, 10f, 10f, new Color(0.99f, 0.88f, 0.32f, 1f));
            DrawEllipse(texture, 34f, 57f, 7f, 7f, new Color(0.99f, 0.82f, 0.23f, 1f));
            DrawEllipse(texture, 61f, 71f, 6f, 5f, new Color(0.88f, 0.18f, 0.18f, 1f));
            DrawTriangle(texture, new Vector2(18f, 42f), new Vector2(29f, 46f), new Vector2(20f, 34f), new Color(1f, 0.66f, 0.08f, 1f));
            DrawEllipse(texture, 55f, 59f, 2.3f, 2.3f, Color.black);
            DrawRect(texture, 33f, 14f, 7f, 9f, new Color(1f, 0.66f, 0.08f, 1f));
            DrawRect(texture, 45f, 14f, 7f, 9f, new Color(1f, 0.66f, 0.08f, 1f));
            DrawTriangle(texture, new Vector2(54f, 40f), new Vector2(68f, 38f), new Vector2(54f, 31f), new Color(0.85f, 0.34f, 0.08f, 1f));
        }

        private void DrawBarrelTexture(Texture2D texture)
        {
            FillTexture(texture, new Color(0f, 0f, 0f, 0f));

            DrawEllipse(texture, 48f, 48f, 23f, 26f, new Color(0.62f, 0.36f, 0.15f, 1f));
            DrawEllipse(texture, 48f, 48f, 19f, 22f, new Color(0.74f, 0.46f, 0.22f, 1f));
            DrawRect(texture, 23f, 32f, 50f, 9f, new Color(0.39f, 0.22f, 0.11f, 1f));
            DrawRect(texture, 23f, 53f, 50f, 9f, new Color(0.39f, 0.22f, 0.11f, 1f));
            DrawEllipse(texture, 48f, 22f, 19f, 6f, new Color(0.55f, 0.31f, 0.13f, 1f));
            DrawEllipse(texture, 48f, 74f, 19f, 6f, new Color(0.47f, 0.27f, 0.1f, 1f));
        }

        private void FillTexture(Texture2D texture, Color color)
        {
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }

        private void DrawEllipse(Texture2D texture, float centerX, float centerY, float radiusX, float radiusY, Color color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(centerX - radiusX - 1f));
            int maxX = Mathf.Min(texture.width - 1, Mathf.CeilToInt(centerX + radiusX + 1f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(centerY - radiusY - 1f));
            int maxY = Mathf.Min(texture.height - 1, Mathf.CeilToInt(centerY + radiusY + 1f));

            float invX = 1f / Mathf.Max(0.001f, radiusX);
            float invY = 1f / Mathf.Max(0.001f, radiusY);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = (x - centerX) * invX;
                    float dy = (y - centerY) * invY;
                    if (dx * dx + dy * dy <= 1f)
                    {
                        texture.SetPixel(x, y, color);
                    }
                }
            }
        }

        private void DrawRect(Texture2D texture, float centerX, float centerY, float width, float height, Color color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(centerX - width * 0.5f));
            int maxX = Mathf.Min(texture.width - 1, Mathf.CeilToInt(centerX + width * 0.5f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(centerY - height * 0.5f));
            int maxY = Mathf.Min(texture.height - 1, Mathf.CeilToInt(centerY + height * 0.5f));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }

        private void DrawTriangle(Texture2D texture, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int maxX = Mathf.Min(texture.width - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
            int maxY = Mathf.Min(texture.height - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (PointInTriangle(new Vector2(x + 0.5f, y + 0.5f), a, b, c))
                    {
                        texture.SetPixel(x, y, color);
                    }
                }
            }
        }

        private bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float s1 = Sign(p, a, b);
            float s2 = Sign(p, b, c);
            float s3 = Sign(p, c, a);
            bool hasNeg = (s1 < 0f) || (s2 < 0f) || (s3 < 0f);
            bool hasPos = (s1 > 0f) || (s2 > 0f) || (s3 > 0f);
            return !(hasNeg && hasPos);
        }

        private float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }

        private void CleanupGeneratedAssets()
        {
            DestroyTexture(ref mSquareSprite);
            DestroyTexture(ref mCircleSprite);
            DestroyTexture(ref mGooseSprite);
            DestroyTexture(ref mChickenSprite);
            DestroyTexture(ref mBarrelSprite);
        }

        private void DestroyTexture(ref Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            Texture2D texture = sprite.texture;
            Destroy(sprite);
            if (texture != null)
            {
                Destroy(texture);
            }

            sprite = null;
        }
    }

#if UNITY_EDITOR
    public static class GooseMergeDemoMenu
    {
        [MenuItem("Tools/Demo/Goose Merge Demo")]
        public static void OpenDemoScene()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/GooseMergeDemo.unity");
        }
    }
#endif
}
