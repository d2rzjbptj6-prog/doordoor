using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private enum EntityKind { Gate, ElementGate, WeaponRack, Chicken, Bullet, Effect }
        private enum ElementKind { None, Fire, Lightning, Ice }
        private enum WeaponKind { Slingshot, Bow, Staff }
        private enum ChickenKind { Normal, Fat, Fast, Boss }
        private enum AttackKind { Normal, Fire, Lightning, Ice, Overload, Vaporize, FrostThunder, Laser }
        private enum EventKind { Gate, ElementGate, WeaponRack, ChickenGroup }

        private sealed class LevelEvent
        {
            public float Time;
            public EventKind Kind;
            public int Lane;
            public int Value;
            public ElementKind Element;
            public WeaponKind Weapon;
            public ChickenKind Chicken;
            public int Count;
        }

        private sealed class LevelPlan
        {
            public string Name;
            public string Tip;
            public readonly List<LevelEvent> Events = new List<LevelEvent>();
        }

        private sealed class Entity
        {
            public EntityKind Kind;
            public GameObject Root;
            public Transform Transform;
            public SpriteRenderer Body;
            public TextMesh Label;
            public SpriteRenderer HealthBack;
            public SpriteRenderer HealthFill;
            public float Speed;
            public float Life;
            public float BurnDps;
            public float BurnTick;
            public float SlowTimer;
            public float MeleeTimer;
            public int Amount;
            public int Health;
            public int MaxHealth;
            public int Damage;
            public int PierceLeft;
            public bool Consumed;
            public bool Unlocked;
            public ElementKind Element;
            public WeaponKind Weapon;
            public ChickenKind Chicken;
            public AttackKind Attack;
        }

        private sealed class GooseView
        {
            public GameObject Root;
            public SpriteRenderer Renderer;
            public float ShootTimer;
        }

        private const float PlayerY = -3.65f;
        private const float SpawnY = 7.1f;
        private const float DespawnY = -7.35f;
        private const float BaseFallSpeed = 3.15f;
        private const float BulletSpeed = 7.6f;
        private const float PlayerMoveSpeed = 8.2f;
        private const float GooseScale = 0.42f;
        private const float GooseStackLift = 0.13f;
        private const float EnemyMeleeY = PlayerY + 0.42f;
        private const float EnemyMeleeCooldown = 0.5f;
        private const int MaxGooseSlots = 7;

        private static readonly float[] LaneXs = { -1.35f, 0f, 1.35f };
        private static readonly Vector3[] GooseOffsets =
        {
            new Vector3(0f, 0.02f, 0f),
            new Vector3(0f, 0.58f, 0f),
            new Vector3(0.48f, 0.29f, 0f),
            new Vector3(0.48f, -0.29f, 0f),
            new Vector3(0f, -0.58f, 0f),
            new Vector3(-0.48f, -0.29f, 0f),
            new Vector3(-0.48f, 0.29f, 0f),
        };

        private Camera mCamera;
        private Transform mWorldRoot;
        private Transform mBackgroundRoot;
        private Transform mEntityRoot;
        private Transform mPlayerRoot;
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

        private readonly List<Entity> mEntities = new List<Entity>();
        private readonly List<GooseView> mGooseViews = new List<GooseView>();
        private readonly List<LevelPlan> mLevels = new List<LevelPlan>();

        private Sprite mSquareSprite;
        private Sprite mCircleSprite;
        private Sprite mBackgroundSprite;
        private Sprite mGooseSlingshotSprite;
        private Sprite mGooseBowSprite;
        private Sprite mGooseStaffSprite;
        private Sprite mChickenSprite;
        private Sprite mFatChickenSprite;
        private Sprite mNormalGateSprite;
        private Sprite mFireGateSprite;
        private Sprite mLightningGateSprite;
        private Sprite mIceGateSprite;
        private Sprite mBowRackSprite;
        private Sprite mStaffRackSprite;

        private int mLevelIndex;
        private int mEventIndex;
        private int mGooseCount;
        private int mFireLevel;
        private int mLightningLevel;
        private int mIceLevel;
        private float mElapsed;
        private float mScore;
        private float mTargetX;
        private bool mGameOver;
        private bool mVictory;
        private bool mBooted;
        private WeaponKind mWeapon = WeaponKind.Slingshot;

        private void Awake()
        {
            Boot();
        }

        private void OnDestroy()
        {
            DestroyGeneratedSprite(ref mSquareSprite);
            DestroyGeneratedSprite(ref mCircleSprite);
        }

        private void Update()
        {
            if (!mBooted)
            {
                return;
            }

            HandleInput();
            if (!mGameOver)
            {
                mElapsed += Time.deltaTime;
                UpdatePlayer();
                UpdateShooting();
                UpdateEntities();
                UpdateStatusDamage();
                UpdateSpawning();
                UpdateScore();
                TryResolveVictory();
            }
            else
            {
                UpdateEffects();
            }

            RefreshGooseFormation();
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
            BuildLevels();
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
            mCamera.backgroundColor = new Color(0.68f, 0.92f, 1f);
            cameraGo.AddComponent<AudioListener>();
        }

        private void SetupSprites()
        {
            mSquareSprite = CreateSprite(CreateTexture(64, 64, t => FillTexture(t, Color.white)), 32f);
            mCircleSprite = CreateSprite(CreateTexture(64, 64, DrawCircleTexture), 32f);
            mBackgroundSprite = LoadArtSprite("sheet_01", 160f);
            mGooseSlingshotSprite = LoadArtSprite("sheet_04", 340f);
            mGooseBowSprite = LoadArtSprite("sheet_10", 340f);
            mGooseStaffSprite = LoadArtSprite("sheet_14", 340f);
            mNormalGateSprite = LoadArtSprite("sheet_25", 290f);
            mFireGateSprite = LoadArtSprite("sheet_31", 310f);
            mLightningGateSprite = LoadArtSprite("sheet_34", 310f);
            mIceGateSprite = LoadArtSprite("sheet_37", 310f);
            mBowRackSprite = LoadArtSprite("sheet_49", 330f);
            mStaffRackSprite = LoadArtSprite("sheet_52", 330f);
            mChickenSprite = LoadArtSprite("sheet_73", 430f);
            mFatChickenSprite = LoadArtSprite("sheet_78", 430f);

            if (mGooseSlingshotSprite == null)
            {
                mGooseSlingshotSprite = CreateSprite(CreateTexture(96, 96, DrawGooseTexture), 32f);
                mGooseBowSprite = mGooseSlingshotSprite;
                mGooseStaffSprite = mGooseSlingshotSprite;
            }
            if (mChickenSprite == null)
            {
                mChickenSprite = CreateSprite(CreateTexture(96, 96, DrawChickenTexture), 32f);
                mFatChickenSprite = mChickenSprite;
            }
        }

        private Sprite LoadArtSprite(string name, float pixelsPerUnit)
        {
            Texture2D texture = Resources.Load<Texture2D>("Art/" + name);
            if (texture == null)
            {
                return null;
            }
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        private void BuildLevels()
        {
            mLevels.Clear();
            mLevels.Add(Level("第1关：先打门", "开枪打 + 门会把数字打大，鹅身体穿过去才加人。",
                C(1f, 1, ChickenKind.Normal, 3), G(3f, 0, 4), G(3f, 2, 8), C(6f, 1, ChickenKind.Normal, 4),
                G(8f, 1, 6), G(11f, 1, 12), G(12f, 0, 14), G(12f, 2, 10), G(13.3f, 1, 16),
                G(16f, 1, 10), C(17f, 0, ChickenKind.Normal, 4), C(17f, 1, ChickenKind.Normal, 6), C(17f, 2, ChickenKind.Normal, 4), C(23f, 1, ChickenKind.Normal, 8)));
            mLevels.Add(Level("第2关：武器架", "木桶已替换成武器架。打爆武器架才换弓或法杖。",
                G(0.5f, 1, 8), C(3f, 1, ChickenKind.Normal, 5), R(4.5f, 0, WeaponKind.Bow), G(4.5f, 2, 12),
                G(8f, 1, 10), R(11f, 1, WeaponKind.Bow), G(12f, 1, 14), R(13.3f, 1, WeaponKind.Staff),
                G(15f, 0, 16), G(15f, 2, 12), G(16f, 1, 10), C(17f, 0, ChickenKind.Normal, 5), C(17f, 1, ChickenKind.Normal, 8), C(17f, 2, ChickenKind.Normal, 5), C(23f, 1, ChickenKind.Fat, 1)));
            mLevels.Add(Level("第3-1关：火门", "先打碎火门锁，再穿门获得 1 层火。火适合烧大肥鸡。",
                G(0.5f, 0, 8), G(0.5f, 2, 6), C(3f, 1, ChickenKind.Normal, 4), R(4.5f, 1, WeaponKind.Bow),
                E(7f, 1, ElementKind.Fire), C(10f, 1, ChickenKind.Fat, 1), G(11f, 1, 14), G(12f, 0, 12), R(12f, 2, WeaponKind.Staff), G(13.5f, 1, 16),
                G(16f, 1, 8), C(17f, 0, ChickenKind.Normal, 4), C(17f, 1, ChickenKind.Normal, 6), C(17f, 2, ChickenKind.Normal, 4), C(23f, 1, ChickenKind.Fat, 1)));
            mLevels.Add(Level("第3-2关：雷门", "先打碎雷门锁，再穿门获得 1 层雷。雷会跳到附近鸡身上。",
                G(0.5f, 0, 8), G(0.5f, 2, 6), C(3f, 1, ChickenKind.Normal, 4), R(4.5f, 1, WeaponKind.Bow),
                E(7f, 1, ElementKind.Lightning), C(10f, 0, ChickenKind.Normal, 3), C(10f, 1, ChickenKind.Normal, 3), C(10f, 2, ChickenKind.Normal, 3),
                G(11f, 1, 14), R(12f, 0, WeaponKind.Staff), G(12f, 2, 12), G(13.5f, 1, 16), G(16f, 1, 8), C(17f, 0, ChickenKind.Normal, 6), C(17f, 1, ChickenKind.Normal, 8), C(17f, 2, ChickenKind.Normal, 6), C(23f, 1, ChickenKind.Fat, 1)));
            mLevels.Add(Level("第4-1关：火变强", "重复穿过不同火门，火从 1 层升到 3 层。",
                G(0.5f, 1, 8), C(3f, 1, ChickenKind.Normal, 4), E(4.5f, 1, ElementKind.Fire), C(7f, 1, ChickenKind.Fat, 1), R(8.5f, 1, WeaponKind.Bow),
                E(11f, 1, ElementKind.Fire), G(12f, 0, 14), R(12f, 2, WeaponKind.Staff), E(13.5f, 1, ElementKind.Fire), G(15f, 1, 16), G(16f, 1, 8),
                C(17f, 0, ChickenKind.Normal, 4), C(17f, 1, ChickenKind.Fat, 1), C(17f, 2, ChickenKind.Normal, 4), C(23f, 0, ChickenKind.Normal, 5), C(23f, 1, ChickenKind.Fat, 1), C(23f, 2, ChickenKind.Normal, 5)));
            mLevels.Add(Level("第4-2关：超载", "火和雷同时存在后，只打超载爆炸，不叠播多套元素。",
                G(0.5f, 1, 8), C(3f, 1, ChickenKind.Normal, 4), E(4.5f, 0, ElementKind.Fire), E(4.5f, 2, ElementKind.Lightning),
                C(7f, 1, ChickenKind.Normal, 6), R(8.5f, 1, WeaponKind.Bow), E(11.5f, 0, ElementKind.Fire), E(11.5f, 2, ElementKind.Lightning),
                C(14f, 0, ChickenKind.Normal, 4), C(14f, 1, ChickenKind.Normal, 6), C(14f, 2, ChickenKind.Normal, 4), G(15f, 1, 16), G(16f, 1, 8), C(17f, 0, ChickenKind.Normal, 6), C(17f, 1, ChickenKind.Normal, 8), C(17f, 2, ChickenKind.Normal, 6), C(24f, 1, ChickenKind.Fat, 1)));
            mLevels.Add(Level("第4-3关：雷变强", "重复穿过雷门，跳电次数和范围提高。",
                G(0.5f, 1, 8), C(3f, 1, ChickenKind.Normal, 4), E(4.5f, 1, ElementKind.Lightning), C(7f, 0, ChickenKind.Normal, 3), C(7f, 1, ChickenKind.Normal, 3), C(7f, 2, ChickenKind.Normal, 3),
                R(8.5f, 1, WeaponKind.Bow), E(11f, 1, ElementKind.Lightning), R(12f, 0, WeaponKind.Staff), G(12f, 2, 14), E(13.5f, 1, ElementKind.Lightning), G(15f, 1, 16), G(16f, 1, 8), C(17f, 0, ChickenKind.Normal, 6), C(17f, 1, ChickenKind.Normal, 8), C(17f, 2, ChickenKind.Normal, 6), C(24f, 1, ChickenKind.Fat, 1)));
            mLevels.Add(Level("第5关：正式关卡", "开放冰、蒸发、霜雷碎、激光、减人数门、快鸡和 Boss。",
                G(0.5f, 0, 10), G(0.5f, 2, 6), C(3f, 0, ChickenKind.Normal, 3), C(3f, 1, ChickenKind.Fat, 1), C(3f, 2, ChickenKind.Fast, 1),
                E(4.8f, 0, ElementKind.Fire), E(4.8f, 1, ElementKind.Lightning), E(4.8f, 2, ElementKind.Ice), C(7f, 0, ChickenKind.Normal, 4), C(7f, 1, ChickenKind.Fat, 1), C(7f, 2, ChickenKind.Fast, 1),
                G(8f, 1, 12), G(9f, 0, 14), G(9f, 2, 10), R(10.5f, 1, WeaponKind.Bow), E(12f, 0, ElementKind.Fire), G(12f, 1, 16), E(12f, 2, ElementKind.Lightning),
                C(13f, 1, ChickenKind.Normal, 6), R(14f, 1, WeaponKind.Staff), E(15f, 0, ElementKind.Ice), E(15f, 2, ElementKind.Fire), G(16f, 0, 18), G(16f, 2, -8),
                G(17f, 1, 12), E(18f, 0, ElementKind.Lightning), E(18f, 1, ElementKind.Ice), E(18f, 2, ElementKind.Fire), C(19f, 0, ChickenKind.Normal, 6), C(19f, 1, ChickenKind.Normal, 8), C(19f, 2, ChickenKind.Normal, 6),
                C(24f, 0, ChickenKind.Fast, 1), C(24f, 1, ChickenKind.Fat, 1), C(24f, 2, ChickenKind.Fast, 1), C(27f, 1, ChickenKind.Boss, 1)));
        }

        private LevelPlan Level(string name, string tip, params LevelEvent[] events)
        {
            LevelPlan level = new LevelPlan { Name = name, Tip = tip };
            level.Events.AddRange(events);
            level.Events.Sort((a, b) => a.Time.CompareTo(b.Time));
            return level;
        }

        private LevelEvent G(float time, int lane, int value) { return new LevelEvent { Time = time, Lane = lane, Value = value, Kind = EventKind.Gate }; }
        private LevelEvent E(float time, int lane, ElementKind element) { return new LevelEvent { Time = time, Lane = lane, Element = element, Kind = EventKind.ElementGate }; }
        private LevelEvent R(float time, int lane, WeaponKind weapon) { return new LevelEvent { Time = time, Lane = lane, Weapon = weapon, Kind = EventKind.WeaponRack }; }
        private LevelEvent C(float time, int lane, ChickenKind chicken, int count) { return new LevelEvent { Time = time, Lane = lane, Chicken = chicken, Count = count, Kind = EventKind.ChickenGroup }; }

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
            BuildBackground();
            CreateSpriteObject("Shadow", mPlayerRoot, mCircleSprite, new Color(0f, 0f, 0f, 0.2f), new Vector3(0f, -0.22f, 0f), new Vector3(1.35f, 0.33f, 1f), 1);
        }

        private void BuildBackground()
        {
            if (mBackgroundSprite != null)
            {
                CreateSpriteObject("PastoralRoad", mBackgroundRoot, mBackgroundSprite, Color.white, Vector3.zero, new Vector3(1.02f, 1.02f, 1f), -120);
            }
            else
            {
                CreateSpriteObject("Sky", mBackgroundRoot, mSquareSprite, new Color(0.70f, 0.92f, 1f), new Vector3(0f, 1f, 0f), new Vector3(8f, 13f, 1f), -120);
                CreateSpriteObject("Path", mBackgroundRoot, mSquareSprite, new Color(0.94f, 0.82f, 0.55f), new Vector3(0f, -1.5f, 0f), new Vector3(4.1f, 11f, 1f), -100);
            }
            CreateSpriteObject("LaneLeft", mBackgroundRoot, mSquareSprite, new Color(1f, 1f, 1f, 0.12f), new Vector3(-1.35f, -0.8f, 0f), new Vector3(0.04f, 10.6f, 1f), -90);
            CreateSpriteObject("LaneRight", mBackgroundRoot, mSquareSprite, new Color(1f, 1f, 1f, 0.12f), new Vector3(1.35f, -0.8f, 0f), new Vector3(0.04f, 10.6f, 1f), -90);
        }

        private void SetupHud()
        {
            GameObject canvasGo = new GameObject("HUD");
            canvasGo.AddComponent<RectTransform>();
            mHudCanvas = canvasGo.AddComponent<Canvas>();
            canvasGo.AddComponent<GraphicRaycaster>();
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            mHudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mHudCanvas.sortingOrder = 200;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.matchWidthOrHeight = 0.65f;
            EnsureEventSystem();

            mScoreText = CreateHudText("ScoreText", mHudCanvas.transform, new Vector2(30f, -28f), TextAnchor.UpperLeft, 28, new Color(0.12f, 0.18f, 0.13f));
            mCountText = CreateHudText("CountText", mHudCanvas.transform, new Vector2(-30f, -28f), TextAnchor.UpperRight, 28, new Color(0.12f, 0.18f, 0.13f));
            mWaveText = CreateHudText("WaveText", mHudCanvas.transform, new Vector2(0f, -26f), TextAnchor.UpperCenter, 25, new Color(0.18f, 0.13f, 0.08f));
            mHintText = CreateHudText("HintText", mHudCanvas.transform, new Vector2(0f, 64f), TextAnchor.LowerCenter, 23, new Color(0.12f, 0.19f, 0.12f));
            mHintText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            mHintText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            mHintText.rectTransform.pivot = new Vector2(0.5f, 0f);
            mHintText.rectTransform.sizeDelta = new Vector2(660f, 100f);
            CreatePlayerHealthHud();
            CreateGameOverHud();
        }

        private void CreateGameOverHud()
        {
            mGameOverPanel = new GameObject("GameOverPanel");
            mGameOverPanel.AddComponent<RectTransform>();
            mGameOverPanel.transform.SetParent(mHudCanvas.transform, false);
            Image panelImage = mGameOverPanel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.42f);
            RectTransform panelRt = mGameOverPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            GameObject card = new GameObject("CenterCard");
            card.AddComponent<RectTransform>();
            card.transform.SetParent(mGameOverPanel.transform, false);
            card.AddComponent<Image>().color = new Color(0.98f, 0.95f, 0.87f, 0.95f);
            RectTransform cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(560f, 300f);

            mGameOverText = CreateHudText("GameOverText", card.transform, new Vector2(0f, 48f), TextAnchor.MiddleCenter, 38, new Color(0.18f, 0.13f, 0.09f));
            mGameOverSubText = CreateHudText("GameOverSubText", card.transform, new Vector2(0f, -8f), TextAnchor.MiddleCenter, 23, new Color(0.23f, 0.19f, 0.14f));
            Button button = CreateButton(card.transform, "RestartButton", "继续", new Vector2(0f, -84f), new Vector2(220f, 72f), new Color(0.24f, 0.62f, 0.35f));
            button.onClick.AddListener(OnContinueButton);
            mGameOverPanel.SetActive(false);
        }

        private void OnContinueButton()
        {
            if (mVictory)
            {
                mLevelIndex = (mLevelIndex + 1) % mLevels.Count;
            }
            RestartRun();
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }
            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private void RestartRun()
        {
            ClearEntities();
            mGooseCount = 1;
            mFireLevel = 0;
            mLightningLevel = 0;
            mIceLevel = 0;
            mWeapon = WeaponKind.Slingshot;
            mEventIndex = 0;
            mElapsed = 0f;
            mScore = 0f;
            mTargetX = 0f;
            mGameOver = false;
            mVictory = false;
            mPlayerRoot.position = new Vector3(0f, PlayerY, 0f);
            RefreshGooseFormation();
            mGameOverPanel.SetActive(false);
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

        private void UpdatePlayer()
        {
            float nextX = Mathf.MoveTowards(mPlayerRoot.position.x, mTargetX, PlayerMoveSpeed * Time.deltaTime);
            mPlayerRoot.position = new Vector3(nextX, PlayerY, 0f);
        }

        private void UpdateScore()
        {
            mScore += Time.deltaTime * (10f + mGooseCount * 0.08f);
        }

        private void UpdateShooting()
        {
            int activeViews = Mathf.Min(mGooseViews.Count, mGooseCount);
            for (int i = 0; i < activeViews; i++)
            {
                GooseView view = mGooseViews[i];
                if (view.Root == null || !view.Root.activeSelf)
                {
                    continue;
                }
                view.ShootTimer -= Time.deltaTime;
                if (view.ShootTimer > 0f)
                {
                    continue;
                }
                FireBullet(view.Root.transform.position + new Vector3(0f, 0.32f, 0f));
                view.ShootTimer = GetWeaponCooldown() + UnityEngine.Random.Range(0f, 0.08f) + (i / MaxGooseSlots) * 0.04f;
            }
        }

        private void FireBullet(Vector3 origin)
        {
            AttackKind attack = ChooseAttack();
            if (attack == AttackKind.Laser)
            {
                FireLaser(origin);
                return;
            }
            Entity bullet = CreateEntity(EntityKind.Bullet, "Shot_" + Time.frameCount, NearestLane(origin.x), origin.y, BulletSpeed);
            bullet.Transform.position = origin;
            bullet.Body.sprite = mWeapon == WeaponKind.Bow ? mSquareSprite : mCircleSprite;
            bullet.Body.color = GetAttackColor(attack);
            bullet.Body.transform.localScale = mWeapon == WeaponKind.Staff ? new Vector3(0.25f, 0.25f, 1f) : new Vector3(0.13f, 0.22f, 1f);
            bullet.Body.sortingOrder = 32;
            bullet.Damage = GetWeaponDamage();
            bullet.Attack = attack;
            bullet.PierceLeft = mWeapon == WeaponKind.Bow ? 1 : 0;
            mEntities.Add(bullet);
        }

        private void FireLaser(Vector3 origin)
        {
            float width = mWeapon == WeaponKind.Staff ? 0.42f : 0.22f;
            GameObject beam = CreateSpriteObject("Laser", mEntityRoot, mSquareSprite, new Color(0.55f, 0.93f, 1f, 0.72f), new Vector3(origin.x, 1.1f, 0f), new Vector3(width, 7.5f, 1f), 45);
            mEntities.Add(new Entity { Kind = EntityKind.Effect, Root = beam, Transform = beam.transform, Body = beam.GetComponent<SpriteRenderer>(), Life = 0.12f });
            for (int i = 0; i < mEntities.Count; i++)
            {
                Entity target = mEntities[i];
                if (target.Kind == EntityKind.Chicken && !target.Consumed && target.Transform.position.y > origin.y && Mathf.Abs(target.Transform.position.x - origin.x) <= 0.42f)
                {
                    target.SlowTimer = Mathf.Max(target.SlowTimer, 1.4f);
                    ApplyChickenDamage(target, Mathf.Max(GetWeaponDamage(), Mathf.RoundToInt(target.MaxHealth * 0.12f)), AttackKind.Laser);
                }
            }
        }

        private void UpdateSpawning()
        {
            LevelPlan level = mLevels[mLevelIndex];
            while (mEventIndex < level.Events.Count && mElapsed >= level.Events[mEventIndex].Time)
            {
                SpawnLevelEvent(level.Events[mEventIndex]);
                mEventIndex++;
            }
        }

        private void SpawnLevelEvent(LevelEvent evt)
        {
            if (evt.Kind == EventKind.Gate)
            {
                SpawnGate(evt.Lane, evt.Value);
            }
            else if (evt.Kind == EventKind.ElementGate)
            {
                SpawnElementGate(evt.Lane, evt.Element);
            }
            else if (evt.Kind == EventKind.WeaponRack)
            {
                SpawnWeaponRack(evt.Lane, evt.Weapon);
            }
            else
            {
                for (int i = 0; i < evt.Count; i++)
                {
                    SpawnChicken(evt.Lane, evt.Chicken, SpawnY + i * 0.32f, (i % 3 - 1) * 0.18f);
                }
            }
        }

        private void UpdateEntities()
        {
            Rect playerRect = GetPlayerRect();
            for (int i = mEntities.Count - 1; i >= 0; i--)
            {
                Entity entity = mEntities[i];
                if (entity.Kind == EntityKind.Bullet)
                {
                    entity.Transform.position += Vector3.up * entity.Speed * Time.deltaTime;
                    TryResolveBulletHit(entity);
                    entity.Consumed |= entity.Transform.position.y > SpawnY + 0.6f;
                }
                else if (entity.Kind == EntityKind.Effect)
                {
                    UpdateEffect(entity);
                }
                else
                {
                    UpdateFallingEntity(entity, playerRect);
                }

                if (entity.Consumed || entity.Transform.position.y < DespawnY)
                {
                    if (!mGameOver && entity.Kind == EntityKind.Chicken && !entity.Consumed)
                    {
                        SetGameOver(false, "有鸡越过鹅群，本关失败");
                    }
                    Destroy(entity.Root);
                    mEntities.RemoveAt(i);
                }
            }
        }

        private void UpdateFallingEntity(Entity entity, Rect playerRect)
        {
            float speed = entity.Kind == EntityKind.Chicken && entity.SlowTimer > 0f ? entity.Speed * 0.35f : entity.Speed;
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
                entity.Transform.position += Vector3.down * speed * Time.deltaTime;
            }

            if (entity.Kind == EntityKind.Gate && playerRect.Overlaps(GetEntityRect(entity)))
            {
                ResolveGate(entity);
                entity.Consumed = true;
            }
            else if (entity.Kind == EntityKind.ElementGate && playerRect.Overlaps(GetEntityRect(entity)))
            {
                if (entity.Unlocked)
                {
                    AddElement(entity.Element);
                    SpawnText(entity.Transform.position, ElementName(entity.Element) + "+1", GetElementColor(entity.Element), 0.42f);
                    entity.Consumed = true;
                }
                else
                {
                    mTargetX = Mathf.Clamp(mTargetX + Mathf.Sign(mPlayerRoot.position.x - entity.Transform.position.x + 0.01f) * 0.65f, GetLeftBound(), GetRightBound());
                }
            }
        }

        private void UpdateEffect(Entity entity)
        {
            entity.Life -= Time.deltaTime;
            if (entity.Body != null)
            {
                Color color = entity.Body.color;
                color.a = Mathf.Clamp01(entity.Life * 5f);
                entity.Body.color = color;
            }
            entity.Consumed = entity.Life <= 0f;
        }

        private void UpdateEffects()
        {
            for (int i = mEntities.Count - 1; i >= 0; i--)
            {
                if (mEntities[i].Kind == EntityKind.Effect)
                {
                    UpdateEffect(mEntities[i]);
                    if (mEntities[i].Consumed)
                    {
                        Destroy(mEntities[i].Root);
                        mEntities.RemoveAt(i);
                    }
                }
            }
        }

        private void UpdateStatusDamage()
        {
            for (int i = 0; i < mEntities.Count; i++)
            {
                Entity entity = mEntities[i];
                if (entity.Kind != EntityKind.Chicken || entity.Consumed)
                {
                    continue;
                }
                if (entity.SlowTimer > 0f)
                {
                    entity.SlowTimer -= Time.deltaTime;
                    entity.Body.color = Color.Lerp(entity.Body.color, new Color(0.7f, 0.93f, 1f), Time.deltaTime * 7f);
                }
                if (entity.BurnDps > 0f)
                {
                    entity.BurnTick += Time.deltaTime;
                    if (entity.BurnTick >= 0.25f)
                    {
                        entity.BurnTick = 0f;
                        ApplyChickenDamage(entity, Mathf.Max(1, Mathf.RoundToInt(entity.MaxHealth * entity.BurnDps * 0.25f)), mIceLevel > 0 ? AttackKind.Vaporize : AttackKind.Fire);
                    }
                }
            }
        }

        private bool TryResolveBulletHit(Entity bullet)
        {
            Rect bulletRect = GetEntityRect(bullet);
            for (int i = mEntities.Count - 1; i >= 0; i--)
            {
                Entity target = mEntities[i];
                if (target == bullet || target.Consumed || target.Kind == EntityKind.Bullet || target.Kind == EntityKind.Effect || !bulletRect.Overlaps(GetEntityRect(target)))
                {
                    continue;
                }
                ResolveBulletTarget(bullet, target);
                if (bullet.PierceLeft > 0 && target.Kind == EntityKind.Chicken)
                {
                    bullet.PierceLeft--;
                    return false;
                }
                bullet.Consumed = true;
                return true;
            }
            return false;
        }

        private void ResolveBulletTarget(Entity bullet, Entity target)
        {
            if (target.Kind == EntityKind.Gate)
            {
                int delta = Mathf.Max(1, bullet.Damage / 10);
                target.Amount = target.Amount >= 0 ? target.Amount + delta : Mathf.Min(-1, target.Amount + delta);
                UpdateGateVisual(target, true);
                mScore += 2f;
            }
            else if (target.Kind == EntityKind.ElementGate)
            {
                if (!target.Unlocked)
                {
                    target.Health -= bullet.Damage;
                    UpdateEnemyHealth(target);
                    if (target.Health <= 0)
                    {
                        target.Unlocked = true;
                        target.Health = 0;
                        UpdateElementGateVisual(target);
                    }
                }
            }
            else if (target.Kind == EntityKind.WeaponRack)
            {
                target.Health -= bullet.Damage;
                UpdateEnemyHealth(target);
                if (target.Health <= 0)
                {
                    mWeapon = target.Weapon;
                    target.Consumed = true;
                    SpawnText(target.Transform.position, WeaponName(target.Weapon), new Color(1f, 0.82f, 0.22f), 0.45f);
                    RefreshGooseWeaponArt();
                }
            }
            else if (target.Kind == EntityKind.Chicken)
            {
                ApplyAttackToChicken(target, bullet.Damage, bullet.Attack);
            }
        }

        private void ApplyAttackToChicken(Entity chicken, int baseDamage, AttackKind attack)
        {
            int damage = baseDamage;
            if (attack == AttackKind.Fire)
            {
                chicken.BurnDps = Mathf.Max(chicken.BurnDps, FireBurnRate());
                damage = Mathf.RoundToInt(baseDamage * 0.55f);
                SpawnText(chicken.Transform.position, "烫", new Color(1f, 0.35f, 0.08f), 0.26f + mFireLevel * 0.05f);
            }
            else if (attack == AttackKind.Lightning)
            {
                ChainLightning(chicken, mLightningLevel);
                damage = Mathf.RoundToInt(baseDamage * 0.85f);
            }
            else if (attack == AttackKind.Ice)
            {
                chicken.SlowTimer = Mathf.Max(chicken.SlowTimer, 1.1f + mIceLevel * 0.45f);
                damage = Mathf.RoundToInt(baseDamage * 0.65f);
                SpawnText(chicken.Transform.position, "冻", new Color(0.45f, 0.85f, 1f), 0.28f);
            }
            else if (attack == AttackKind.Overload)
            {
                damage = Mathf.RoundToInt(baseDamage * 1.15f);
                Explode(chicken.Transform.position, 0.72f + (mFireLevel + mLightningLevel) * 0.08f, Mathf.RoundToInt(baseDamage * 0.8f), AttackKind.Overload);
            }
            else if (attack == AttackKind.Vaporize)
            {
                chicken.BurnDps = Mathf.Max(chicken.BurnDps, FireBurnRate() * 1.65f);
                damage = Mathf.RoundToInt(baseDamage * 1.1f + chicken.MaxHealth * 0.03f);
                SpawnText(chicken.Transform.position, "蒸汽", new Color(0.9f, 0.97f, 1f), 0.32f);
            }
            else if (attack == AttackKind.FrostThunder)
            {
                chicken.SlowTimer = Mathf.Max(chicken.SlowTimer, 1.8f);
                ChainLightning(chicken, 1 + mLightningLevel);
                Explode(chicken.Transform.position, 0.58f, Mathf.RoundToInt(baseDamage * 0.35f), AttackKind.FrostThunder);
            }
            ApplyChickenDamage(chicken, Mathf.Max(1, damage), attack);
        }

        private void ChainLightning(Entity source, int jumps)
        {
            Vector3 current = source.Transform.position;
            List<Entity> chained = new List<Entity> { source };
            for (int step = 0; step < Mathf.Clamp(jumps, 1, 3); step++)
            {
                Entity best = null;
                float bestDistance = 999f;
                for (int i = 0; i < mEntities.Count; i++)
                {
                    Entity candidate = mEntities[i];
                    if (chained.Contains(candidate) || candidate.Kind != EntityKind.Chicken || candidate.Consumed)
                    {
                        continue;
                    }
                    float distance = Vector3.Distance(current, candidate.Transform.position);
                    if (distance < bestDistance && distance < 1.6f + mLightningLevel * 0.25f)
                    {
                        best = candidate;
                        bestDistance = distance;
                    }
                }
                if (best == null)
                {
                    break;
                }
                SpawnBeam(current, best.Transform.position, new Color(1f, 0.9f, 0.1f, 0.85f), 0.08f);
                ApplyChickenDamage(best, Mathf.Max(3, GetWeaponDamage() / 2), AttackKind.Lightning);
                chained.Add(best);
                current = best.Transform.position;
            }
        }

        private void Explode(Vector3 center, float radius, int damage, AttackKind attack)
        {
            Color color = attack == AttackKind.Overload ? new Color(1f, 0.4f, 0.05f, 0.45f) : new Color(0.55f, 0.9f, 1f, 0.42f);
            GameObject flash = CreateSpriteObject("ReactionFlash", mEntityRoot, mCircleSprite, color, center, new Vector3(radius * 2f, radius * 2f, 1f), 40);
            mEntities.Add(new Entity { Kind = EntityKind.Effect, Root = flash, Transform = flash.transform, Body = flash.GetComponent<SpriteRenderer>(), Life = 0.2f });
            for (int i = 0; i < mEntities.Count; i++)
            {
                Entity target = mEntities[i];
                if (target.Kind == EntityKind.Chicken && !target.Consumed && Vector3.Distance(center, target.Transform.position) <= radius)
                {
                    if (attack == AttackKind.FrostThunder)
                    {
                        target.SlowTimer = Mathf.Max(target.SlowTimer, 1.2f);
                    }
                    ApplyChickenDamage(target, damage, attack);
                }
            }
        }

        private void ApplyChickenDamage(Entity chicken, int damage, AttackKind attack)
        {
            if (chicken.Consumed)
            {
                return;
            }
            chicken.Health -= Mathf.Max(1, damage);
            UpdateEnemyHealth(chicken);
            if (chicken.Health <= 0)
            {
                chicken.Consumed = true;
                mScore += chicken.Chicken == ChickenKind.Boss ? 200f : chicken.Chicken == ChickenKind.Fat ? 40f : 12f;
                SpawnDeathEffect(chicken, attack);
            }
        }

        private void ResolveGate(Entity gate)
        {
            if (gate.Amount >= 0)
            {
                AddGoose(gate.Amount);
                SpawnText(gate.Transform.position, "+" + gate.Amount, new Color(0.22f, 0.82f, 0.37f), 0.5f);
                mScore += gate.Amount * 8f;
            }
            else
            {
                int before = mGooseCount;
                mGooseCount = Mathf.Max(1, mGooseCount + gate.Amount);
                SpawnText(gate.Transform.position, "-" + (before - mGooseCount), new Color(0.95f, 0.18f, 0.14f), 0.5f);
            }
        }

        private void AddGoose(int amount)
        {
            mGooseCount += Mathf.Max(0, amount);
            RefreshGooseFormation();
        }

        private void AddElement(ElementKind element)
        {
            if (element == ElementKind.Fire)
            {
                mFireLevel = Mathf.Min(3, mFireLevel + 1);
            }
            else if (element == ElementKind.Lightning)
            {
                mLightningLevel = Mathf.Min(3, mLightningLevel + 1);
            }
            else if (element == ElementKind.Ice)
            {
                mIceLevel = Mathf.Min(3, mIceLevel + 1);
            }
            RefreshGooseWeaponArt();
        }

        private void DamageGoose(int amount)
        {
            mGooseCount -= Mathf.Max(1, amount);
            if (mGooseCount <= 0)
            {
                mGooseCount = 0;
                SetGameOver(false, "鹅的数量归零，本关失败");
            }
        }

        private void SetGameOver(bool victory, string reason)
        {
            mVictory = victory;
            mGameOver = true;
            mGameOverPanel.SetActive(true);
            mGameOverText.text = victory ? "清场胜利" : "本关失败";
            mGameOverSubText.text = reason;
        }

        private void TryResolveVictory()
        {
            if (mGameOver || mEventIndex < mLevels[mLevelIndex].Events.Count)
            {
                return;
            }
            for (int i = 0; i < mEntities.Count; i++)
            {
                EntityKind kind = mEntities[i].Kind;
                if (kind == EntityKind.Chicken || kind == EntityKind.Gate || kind == EntityKind.ElementGate || kind == EntityKind.WeaponRack)
                {
                    return;
                }
            }
            SetGameOver(true, "下一关会重置为 1 只鹅、弹弓、无元素");
        }

        private void RefreshGooseFormation()
        {
            while (mGooseViews.Count < mGooseCount)
            {
                mGooseViews.Add(CreateGooseView(mGooseViews.Count));
            }
            for (int i = 0; i < mGooseViews.Count; i++)
            {
                bool active = i < mGooseCount;
                GooseView view = mGooseViews[i];
                view.Root.SetActive(active);
                if (!active)
                {
                    continue;
                }
                int slotIndex = i % MaxGooseSlots;
                int stackIndex = i / MaxGooseSlots;
                view.Root.transform.localPosition = GooseOffsets[slotIndex] + new Vector3(0f, stackIndex * GooseStackLift, 0f);
                view.Root.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
                view.Renderer.sprite = CurrentGooseSprite();
                view.Renderer.color = CurrentGooseTint();
                view.Renderer.sortingOrder = 20 + slotIndex * 4 + stackIndex;
                view.Root.transform.localScale = Vector3.one * (GooseScale + Mathf.Sin(Time.time * 5f + i * 0.35f) * 0.018f);
            }
            mTargetX = Mathf.Clamp(mTargetX, GetLeftBound(), GetRightBound());
            mPlayerRoot.position = new Vector3(Mathf.Clamp(mPlayerRoot.position.x, GetLeftBound(), GetRightBound()), PlayerY, 0f);
        }

        private GooseView CreateGooseView(int index)
        {
            GameObject go = new GameObject("Goose_" + index);
            go.transform.SetParent(mPlayerRoot, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = CurrentGooseSprite();
            return new GooseView { Root = go, Renderer = renderer, ShootTimer = UnityEngine.Random.Range(0.03f, 0.28f) };
        }

        private void RefreshGooseWeaponArt()
        {
            for (int i = 0; i < mGooseViews.Count; i++)
            {
                mGooseViews[i].Renderer.sprite = CurrentGooseSprite();
                mGooseViews[i].Renderer.color = CurrentGooseTint();
            }
        }

        private void SpawnGate(int lane, int value)
        {
            Entity gate = CreateEntity(EntityKind.Gate, "Gate", lane, SpawnY, BaseFallSpeed);
            gate.Amount = value;
            gate.Body.sprite = mNormalGateSprite != null ? mNormalGateSprite : mSquareSprite;
            gate.Body.color = value < 0 ? new Color(1f, 0.32f, 0.26f) : Color.white;
            gate.Body.transform.localScale = new Vector3(0.68f, 0.68f, 1f);
            gate.Label = CreateWorldLabel(gate.Root.transform, "", new Vector3(0f, 0.38f, 0f), 0.16f, value < 0 ? new Color(0.9f, 0.05f, 0.03f) : new Color(0.1f, 0.28f, 0.12f));
            UpdateGateVisual(gate, false);
            mEntities.Add(gate);
        }

        private void SpawnElementGate(int lane, ElementKind element)
        {
            Entity gate = CreateEntity(EntityKind.ElementGate, ElementName(element) + "Gate", lane, SpawnY, BaseFallSpeed);
            gate.Element = element;
            gate.Health = Mathf.RoundToInt(Mathf.Max(20f, mGooseCount * GetWeaponDamage() * 0.75f));
            gate.MaxHealth = gate.Health;
            gate.Body.sprite = ElementGateSprite(element);
            gate.Body.color = Color.white;
            gate.Body.transform.localScale = new Vector3(0.62f, 0.62f, 1f);
            gate.Label = CreateWorldLabel(gate.Root.transform, "锁", new Vector3(0f, 0.5f, 0f), 0.14f, GetElementColor(element));
            CreateEnemyHealthBar(gate, GetElementColor(element));
            mEntities.Add(gate);
        }

        private void SpawnWeaponRack(int lane, WeaponKind weapon)
        {
            Entity rack = CreateEntity(EntityKind.WeaponRack, WeaponName(weapon) + "Rack", lane, SpawnY, BaseFallSpeed);
            rack.Weapon = weapon;
            rack.Health = Mathf.RoundToInt(Mathf.Max(24f, mGooseCount * GetWeaponDamage() * 0.9f));
            rack.MaxHealth = rack.Health;
            rack.Body.sprite = weapon == WeaponKind.Bow ? mBowRackSprite : mStaffRackSprite;
            rack.Body.color = Color.white;
            rack.Body.transform.localScale = new Vector3(0.58f, 0.58f, 1f);
            rack.Label = CreateWorldLabel(rack.Root.transform, WeaponName(weapon), new Vector3(0f, 0.62f, 0f), 0.12f, new Color(0.25f, 0.15f, 0.04f));
            CreateEnemyHealthBar(rack, new Color(0.94f, 0.65f, 0.18f));
            mEntities.Add(rack);
        }

        private void SpawnChicken(int lane, ChickenKind kind, float y, float sideOffset)
        {
            Entity chicken = CreateEntity(EntityKind.Chicken, kind + "Chicken", lane, y, ChickenSpeed(kind));
            chicken.Transform.position += new Vector3(sideOffset, 0f, 0f);
            chicken.Chicken = kind;
            chicken.Health = ChickenHealth(kind);
            chicken.MaxHealth = chicken.Health;
            chicken.MeleeTimer = EnemyMeleeCooldown;
            chicken.Body.sprite = kind == ChickenKind.Fat || kind == ChickenKind.Boss ? mFatChickenSprite : mChickenSprite;
            chicken.Body.color = kind == ChickenKind.Fast ? new Color(1f, 1f, 0.72f) : Color.white;
            chicken.Body.transform.localScale = ChickenScale(kind);
            chicken.Transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            CreateEnemyHealthBar(chicken, new Color(0.9f, 0.18f, 0.16f));
            mEntities.Add(chicken);
        }

        private Entity CreateEntity(EntityKind kind, string name, int lane, float y, float speed)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(mEntityRoot, false);
            root.transform.position = new Vector3(LaneXs[Mathf.Clamp(lane, 0, LaneXs.Length - 1)], y, 0f);
            SpriteRenderer body = root.AddComponent<SpriteRenderer>();
            body.sprite = mSquareSprite;
            body.color = Color.white;
            body.sortingOrder = kind == EntityKind.Bullet ? 30 : 12;
            return new Entity { Kind = kind, Root = root, Transform = root.transform, Body = body, Speed = speed, Amount = 1, Health = 1, MaxHealth = 1, Damage = 1 };
        }

        private void UpdateGateVisual(Entity gate, bool pop)
        {
            gate.Label.text = gate.Amount >= 0 ? "+" + gate.Amount : gate.Amount.ToString();
            gate.Label.transform.localScale = Vector3.one * (pop ? 1.2f : 1f);
            if (pop)
            {
                SpawnText(gate.Transform.position + Vector3.up * 0.7f, gate.Amount >= 0 ? "+1" : "减弱", gate.Amount >= 0 ? new Color(0.2f, 0.85f, 0.35f) : new Color(1f, 0.28f, 0.18f), 0.24f);
            }
        }

        private void UpdateElementGateVisual(Entity gate)
        {
            gate.Body.color = Color.Lerp(Color.white, GetElementColor(gate.Element), 0.25f);
            gate.Label.text = ElementName(gate.Element);
            gate.Label.color = GetElementColor(gate.Element);
            SpawnText(gate.Transform.position, "锁碎", GetElementColor(gate.Element), 0.42f);
        }

        private void SpawnDeathEffect(Entity chicken, AttackKind attack)
        {
            string label = "倒";
            Color color = new Color(0.95f, 0.78f, 0.24f);
            if (attack == AttackKind.Fire) { label = "烤鸡"; color = new Color(1f, 0.55f, 0.12f); }
            else if (attack == AttackKind.Lightning) { label = "骨架"; color = new Color(0.18f, 0.18f, 0.2f); }
            else if (attack == AttackKind.Ice) { label = "冻鸡"; color = new Color(0.55f, 0.9f, 1f); }
            else if (attack == AttackKind.Overload) { label = "鸡块"; color = new Color(1f, 0.35f, 0.05f); }
            else if (attack == AttackKind.Vaporize) { label = "热汽"; color = new Color(0.9f, 0.95f, 1f); }
            else if (attack == AttackKind.FrostThunder) { label = "冰裂"; color = new Color(0.58f, 0.88f, 1f); }
            else if (attack == AttackKind.Laser) { label = "贯穿"; color = new Color(0.68f, 0.95f, 1f); }
            SpawnText(chicken.Transform.position, label, color, 0.36f);
            GameObject corpse = CreateSpriteObject("Corpse_" + label, mEntityRoot, mCircleSprite, color, chicken.Transform.position + Vector3.down * 0.05f, new Vector3(0.58f, 0.24f, 1f), 6);
            mEntities.Add(new Entity { Kind = EntityKind.Effect, Root = corpse, Transform = corpse.transform, Body = corpse.GetComponent<SpriteRenderer>(), Life = 0.65f });
        }

        private void SpawnBeam(Vector3 a, Vector3 b, Color color, float life)
        {
            Vector3 mid = (a + b) * 0.5f;
            float length = Vector3.Distance(a, b);
            GameObject beam = CreateSpriteObject("Beam", mEntityRoot, mSquareSprite, color, mid, new Vector3(0.06f, length, 1f), 44);
            Vector3 dir = b - a;
            beam.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            mEntities.Add(new Entity { Kind = EntityKind.Effect, Root = beam, Transform = beam.transform, Body = beam.GetComponent<SpriteRenderer>(), Life = life });
        }

        private void SpawnText(Vector3 position, string text, Color color, float size)
        {
            TextMesh label = CreateWorldLabel(mEntityRoot, text, position, size, color);
            mEntities.Add(new Entity { Kind = EntityKind.Effect, Root = label.gameObject, Transform = label.transform, Life = 0.65f });
        }

        private void CreateEnemyHealthBar(Entity entity, Color fillColor)
        {
            GameObject back = CreateSpriteObject("HealthBack", entity.Root.transform, mSquareSprite, new Color(0.08f, 0.08f, 0.08f, 0.62f), new Vector3(0f, 0.78f, 0f), new Vector3(0.62f, 0.08f, 1f), 25);
            GameObject fill = CreateSpriteObject("HealthFill", entity.Root.transform, mSquareSprite, fillColor, new Vector3(0f, 0.78f, 0f), new Vector3(0.62f, 0.08f, 1f), 26);
            entity.HealthBack = back.GetComponent<SpriteRenderer>();
            entity.HealthFill = fill.GetComponent<SpriteRenderer>();
            UpdateEnemyHealth(entity);
        }

        private void UpdateEnemyHealth(Entity entity)
        {
            if (entity.HealthBack == null || entity.HealthFill == null)
            {
                return;
            }
            float percent = entity.MaxHealth <= 0 ? 0f : Mathf.Clamp01(entity.Health / (float)entity.MaxHealth);
            entity.HealthFill.transform.localScale = new Vector3(0.62f * percent, 0.08f, 1f);
            entity.HealthFill.color = Color.Lerp(new Color(0.93f, 0.12f, 0.12f), new Color(0.18f, 0.84f, 0.34f), percent);
            entity.HealthBack.gameObject.SetActive(percent > 0f);
        }

        private Rect GetPlayerRect()
        {
            float height = 1.55f + Mathf.Max(0, Mathf.CeilToInt(mGooseCount / (float)MaxGooseSlots) - 1) * GooseStackLift;
            return new Rect(mPlayerRoot.position.x - 1.0f, PlayerY - 0.12f, 2.0f, height);
        }

        private Rect GetEntityRect(Entity entity)
        {
            Vector3 p = entity.Transform.position;
            if (entity.Kind == EntityKind.Gate || entity.Kind == EntityKind.ElementGate)
            {
                return new Rect(p.x - 0.55f, p.y - 0.8f, 1.1f, 1.6f);
            }
            if (entity.Kind == EntityKind.WeaponRack)
            {
                return new Rect(p.x - 0.48f, p.y - 0.55f, 0.96f, 1.1f);
            }
            if (entity.Kind == EntityKind.Chicken)
            {
                float size = entity.Chicken == ChickenKind.Boss ? 0.95f : entity.Chicken == ChickenKind.Fat ? 0.72f : 0.52f;
                return new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size);
            }
            if (entity.Kind == EntityKind.Bullet)
            {
                return new Rect(p.x - 0.11f, p.y - 0.16f, 0.22f, 0.32f);
            }
            return new Rect(p.x - 0.25f, p.y - 0.25f, 0.5f, 0.5f);
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
                mTargetX = Mathf.Clamp(mTargetX + axis * PlayerMoveSpeed * Time.deltaTime, GetLeftBound(), GetRightBound());
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

        private int NearestLane(float x)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < LaneXs.Length; i++)
            {
                float distance = Mathf.Abs(x - LaneXs[i]);
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private void RefreshHud()
        {
            LevelPlan level = mLevels[mLevelIndex];
            mScoreText.text = "得分 " + Mathf.FloorToInt(mScore);
            mCountText.text = "鹅 " + mGooseCount;
            mWaveText.text = level.Name + " | " + WeaponName(mWeapon) + " 火" + mFireLevel + " 雷" + mLightningLevel + " 冰" + mIceLevel;
            mHintText.text = mGameOver ? (mVictory ? "点击继续进入下一关" : "点击继续重试本关") : level.Tip;
            RefreshPlayerHealthHud();
        }

        private void CreatePlayerHealthHud()
        {
            GameObject panel = new GameObject("PlayerHealthPanel");
            panel.transform.SetParent(mHudCanvas.transform, false);
            RectTransform panelRt = panel.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 1f);
            panelRt.anchorMax = new Vector2(0f, 1f);
            panelRt.pivot = new Vector2(0f, 1f);
            panelRt.anchoredPosition = new Vector2(30f, -72f);
            panelRt.sizeDelta = new Vector2(246f, 38f);
            Image back = panel.AddComponent<Image>();
            back.sprite = mSquareSprite;
            back.color = new Color(0.14f, 0.12f, 0.1f, 0.75f);

            GameObject fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(panel.transform, false);
            RectTransform fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = new Vector2(3f, 0f);
            fillRt.sizeDelta = new Vector2(240f, 32f);
            mPlayerHealthFill = fillGo.AddComponent<Image>();
            mPlayerHealthFill.sprite = mSquareSprite;
            mPlayerHealthText = CreateHudText("PlayerHealthText", panel.transform, new Vector2(10f, -2f), TextAnchor.MiddleLeft, 19, Color.white);
            mPlayerHealthText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            mPlayerHealthText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            mPlayerHealthText.rectTransform.offsetMin = new Vector2(10f, -14f);
            mPlayerHealthText.rectTransform.offsetMax = new Vector2(-10f, 14f);
        }

        private void RefreshPlayerHealthHud()
        {
            float percent = Mathf.Clamp01(mGooseCount / 60f);
            mPlayerHealthFill.rectTransform.sizeDelta = new Vector2(240f * Mathf.Max(0.1f, percent), 32f);
            mPlayerHealthFill.color = Color.Lerp(new Color(0.88f, 0.2f, 0.16f), new Color(0.21f, 0.72f, 0.32f), percent);
            mPlayerHealthText.text = "鹅群血量 " + mGooseCount;
        }

        private GameObject CreateSpriteObject(string name, Transform parent, Sprite sprite, Color color, Vector3 localPos, Vector3 localScale, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return go;
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
            rt.sizeDelta = new Vector2(660f, 80f);
            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
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
            image.color = color;
            Button button = go.AddComponent<Button>();
            Text text = CreateHudText("LabelText", go.transform, Vector2.zero, TextAnchor.MiddleCenter, 28, Color.white);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
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
            go.GetComponent<MeshRenderer>().sortingOrder = 90;
            return mesh;
        }

        private float GetWeaponCooldown()
        {
            return mWeapon == WeaponKind.Bow ? 0.4f : mWeapon == WeaponKind.Staff ? 0.7f : 0.6f;
        }

        private int GetWeaponDamage()
        {
            return mWeapon == WeaponKind.Bow ? 14 : mWeapon == WeaponKind.Staff ? 28 : 10;
        }

        private float FireBurnRate()
        {
            return mFireLevel >= 3 ? 0.07f : mFireLevel == 2 ? 0.04f : 0.02f;
        }

        private AttackKind ChooseAttack()
        {
            if (mFireLevel > 0 && mLightningLevel > 0 && mIceLevel > 0) return AttackKind.Laser;
            if (mFireLevel > 0 && mLightningLevel > 0) return AttackKind.Overload;
            if (mFireLevel > 0 && mIceLevel > 0) return AttackKind.Vaporize;
            if (mLightningLevel > 0 && mIceLevel > 0) return AttackKind.FrostThunder;
            if (mFireLevel > 0) return AttackKind.Fire;
            if (mLightningLevel > 0) return AttackKind.Lightning;
            if (mIceLevel > 0) return AttackKind.Ice;
            return AttackKind.Normal;
        }

        private Sprite CurrentGooseSprite()
        {
            return mWeapon == WeaponKind.Bow ? (mGooseBowSprite ?? mGooseSlingshotSprite) : mWeapon == WeaponKind.Staff ? (mGooseStaffSprite ?? mGooseSlingshotSprite) : mGooseSlingshotSprite;
        }

        private Color CurrentGooseTint()
        {
            AttackKind attack = ChooseAttack();
            return attack == AttackKind.Normal ? Color.white : Color.Lerp(Color.white, GetAttackColor(attack), 0.24f);
        }

        private Sprite ElementGateSprite(ElementKind element)
        {
            if (element == ElementKind.Fire) return mFireGateSprite ?? mNormalGateSprite;
            if (element == ElementKind.Lightning) return mLightningGateSprite ?? mNormalGateSprite;
            if (element == ElementKind.Ice) return mIceGateSprite ?? mNormalGateSprite;
            return mNormalGateSprite;
        }

        private Vector3 ChickenScale(ChickenKind kind)
        {
            if (kind == ChickenKind.Boss) return new Vector3(0.95f, 0.95f, 1f);
            if (kind == ChickenKind.Fat) return new Vector3(0.7f, 0.7f, 1f);
            if (kind == ChickenKind.Fast) return new Vector3(0.48f, 0.48f, 1f);
            return new Vector3(0.55f, 0.55f, 1f);
        }

        private int ChickenHealth(ChickenKind kind)
        {
            if (kind == ChickenKind.Boss) return 800;
            if (kind == ChickenKind.Fat) return 200;
            if (kind == ChickenKind.Fast) return 30;
            return 20;
        }

        private float ChickenSpeed(ChickenKind kind)
        {
            if (kind == ChickenKind.Fat) return BaseFallSpeed * 0.66f;
            if (kind == ChickenKind.Fast) return BaseFallSpeed * 1.72f;
            if (kind == ChickenKind.Boss) return BaseFallSpeed * 0.48f;
            return BaseFallSpeed;
        }

        private Color GetElementColor(ElementKind element)
        {
            if (element == ElementKind.Fire) return new Color(1f, 0.32f, 0.08f);
            if (element == ElementKind.Lightning) return new Color(1f, 0.9f, 0.06f);
            if (element == ElementKind.Ice) return new Color(0.45f, 0.86f, 1f);
            return Color.white;
        }

        private Color GetAttackColor(AttackKind attack)
        {
            if (attack == AttackKind.Fire) return new Color(1f, 0.35f, 0.08f);
            if (attack == AttackKind.Lightning) return new Color(1f, 0.9f, 0.08f);
            if (attack == AttackKind.Ice) return new Color(0.48f, 0.88f, 1f);
            if (attack == AttackKind.Overload) return new Color(1f, 0.48f, 0.05f);
            if (attack == AttackKind.Vaporize) return new Color(0.9f, 0.96f, 1f);
            if (attack == AttackKind.FrostThunder) return new Color(0.58f, 0.86f, 1f);
            if (attack == AttackKind.Laser) return new Color(0.64f, 0.94f, 1f);
            return new Color(0.36f, 0.24f, 0.14f);
        }

        private string ElementName(ElementKind element)
        {
            if (element == ElementKind.Fire) return "火";
            if (element == ElementKind.Lightning) return "雷";
            if (element == ElementKind.Ice) return "冰";
            return "";
        }

        private string WeaponName(WeaponKind weapon)
        {
            return weapon == WeaponKind.Bow ? "弓" : weapon == WeaponKind.Staff ? "法杖" : "弹弓";
        }

        private float GetLeftBound()
        {
            return -1.57f;
        }

        private float GetRightBound()
        {
            return 1.57f;
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
                    if (dx * dx + dy * dy <= 1f)
                    {
                        texture.SetPixel(x, y, Color.white);
                    }
                }
            }
        }

        private void DrawGooseTexture(Texture2D texture)
        {
            FillTexture(texture, new Color(0f, 0f, 0f, 0f));
            DrawEllipse(texture, 49f, 52f, 20f, 16f, new Color(0.97f, 0.98f, 0.99f));
            DrawEllipse(texture, 31f, 34f, 14f, 12f, new Color(0.97f, 0.98f, 0.99f));
            DrawTriangle(texture, new Vector2(17f, 33f), new Vector2(31f, 37f), new Vector2(22f, 26f), new Color(1f, 0.68f, 0.16f));
            DrawEllipse(texture, 28f, 36f, 2.2f, 2.2f, Color.black);
        }

        private void DrawChickenTexture(Texture2D texture)
        {
            FillTexture(texture, new Color(0f, 0f, 0f, 0f));
            DrawEllipse(texture, 44f, 44f, 20f, 16f, new Color(0.99f, 0.88f, 0.32f));
            DrawEllipse(texture, 59f, 56f, 10f, 10f, new Color(0.99f, 0.88f, 0.32f));
            DrawTriangle(texture, new Vector2(18f, 42f), new Vector2(29f, 46f), new Vector2(20f, 34f), new Color(1f, 0.66f, 0.08f));
            DrawEllipse(texture, 55f, 59f, 2.3f, 2.3f, Color.black);
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
            int minX = Mathf.Max(0, Mathf.FloorToInt(centerX - radiusX));
            int maxX = Mathf.Min(texture.width - 1, Mathf.CeilToInt(centerX + radiusX));
            int minY = Mathf.Max(0, Mathf.FloorToInt(centerY - radiusY));
            int maxY = Mathf.Min(texture.height - 1, Mathf.CeilToInt(centerY + radiusY));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = (x - centerX) / Mathf.Max(0.001f, radiusX);
                    float dy = (y - centerY) / Mathf.Max(0.001f, radiusY);
                    if (dx * dx + dy * dy <= 1f)
                    {
                        texture.SetPixel(x, y, color);
                    }
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
            bool hasNeg = s1 < 0f || s2 < 0f || s3 < 0f;
            bool hasPos = s1 > 0f || s2 > 0f || s3 > 0f;
            return !(hasNeg && hasPos);
        }

        private float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }

        private void DestroyGeneratedSprite(ref Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }
            Texture2D texture = sprite.texture;
            Destroy(sprite);
            if (texture != null && texture.name == "")
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
