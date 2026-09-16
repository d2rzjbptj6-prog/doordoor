using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Video;

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
        private enum EntityKind { Gate, GooseCage, ElementGate, WeaponRack, Chicken, Bullet, Effect }
        private enum ElementKind { None, Fire, Lightning, Ice }
        private enum WeaponKind { Slingshot, Bow, Staff }
        private enum ChickenKind { Normal, Fat, Fast, Boss }
        private enum AttackKind { Normal, Fire, Lightning, Ice, Overload, Vaporize, FrostThunder, Laser }
        private enum EventKind { Gate, ElementGate, WeaponRack, ChickenGroup, GooseCage }

        private sealed class LevelEvent
        {
            public float Time;
            public EventKind Kind;
            public int Lane;
            public int Value;
            public int Health;
            public ElementKind Element;
            public WeaponKind Weapon;
            public ChickenKind Chicken;
            public int Count;
            public float LogicalY;
            public int GateGroup;
        }

        private sealed class LevelPlan
        {
            public string Name;
            public string Tip;
            public readonly List<LevelEvent> Events = new List<LevelEvent>();
        }

        private sealed class ActorConfig
        {
            public int Id;
            public string Name;
            public int BuffId;
            public int ModelId;
            public float BreakRadius;
            public readonly Dictionary<string, float> Stats = new Dictionary<string, float>();
        }

        private sealed class BuffConfig
        {
            public int Id;
            public string Name;
            public int EffectId;
            public int Priority;
            public int PriorityGroup;
            public int FrontBuffId;
            public float BulletDistance;
            public float BulletSpeed;
            public float BreakRadius;
            public float BattleRadius;
        }

        [System.Serializable]
        private sealed class ModelConfigData
        {
            public ActorConfigData[] actors;
            public BuffConfigData[] buffs;
            public MonsterConfigData[] monsters;
        }

        [Serializable]
        private sealed class LevelConfigData
        {
            public LevelData[] levels;
        }

        [Serializable]
        private sealed class LevelData
        {
            public int stageId;
            public string name;
            public string tip;
            public LevelEventData[] events;
        }

        [Serializable]
        private sealed class LevelEventData
        {
            public float time;
            public int kind;
            public int lane;
            public int value;
            public int health;
            public int element;
            public int weapon;
            public int chicken;
            public int count;
            public float y;
        }

        [System.Serializable]
        private sealed class ActorConfigData
        {
            public int id;
            public string name;
            public int buffId;
            public int modelId;
            public float breakValue;
            public StatConfigData[] stats;
        }

        [System.Serializable]
        private sealed class StatConfigData
        {
            public string key;
            public float value;
        }

        [System.Serializable]
        private sealed class BuffConfigData
        {
            public int id;
            public string name;
            public int effectId;
            public int priority;
            public int priorityGroup;
            public int frontBuffId;
            public float bulletDistance;
            public float bulletSpeed;
            public float breakValue;
            public float battleValue;
        }

        [System.Serializable]
        private sealed class MonsterConfigData
        {
            public int id;
            public int stageId;
            public int actorId;
            public string note;
            public int doorGroup;
            public int count;
            public float x;
            public float y;
            public string range;
            public string blood;
        }

        private sealed class Entity
        {
            public EntityKind Kind;
            public GameObject Root;
            public Transform Transform;
            public SpriteRenderer Body;
            public TextMesh Label;
            public TextMesh[] LabelOutlines;
            public TextMesh RewardLabel;
            public SpriteRenderer HealthBack;
            public SpriteRenderer HealthFill;
            public float Speed;
            public float BreakRadius;
            public float LogicalX;
            public float LogicalY;
            public float LogicalOffsetX;
            public float StartY;
            public float Range;
            public float DamageRadius;
            public float Life;
            public float BurnDps;
            public float BurnTick;
            public float SlowTimer;
            public float MeleeTimer;
            public int ActorId;
            public int Amount;
            public int Health;
            public int MaxHealth;
            public int Damage;
            public int GateGroup;
            public float Defense;
            public Vector3 BaseScale;
            public RenderTexture EffectTexture;
            public Material EffectMaterial;
            public VideoPlayer EffectVideoPlayer;
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
            public SpriteRenderer HealthBack;
            public SpriteRenderer HealthFill;
            public float ShootTimer;
            public int Health;
            public int MaxHealth;
            public bool AttackPlaying;
            public bool AttackBulletFired;
            public float AttackTimer;
            public float AttackDuration;
            public float AttackFireTime;
            public int AttackSegmentIndex;
            public GameObject AttackEffectRoot;
            public Renderer AttackRenderer;
            public float AttackSegmentStart;
            public float AttackSourceDuration;
        }

        // The first goose is the shared origin reference for logical and Unity coordinates.
        private const float PlayerY = 0f;
        private const float CameraCenterY = 3.25f;
        private const float CameraHalfHeight = 6.5f;
        private const float SpawnY = CameraCenterY + CameraHalfHeight - 0.4f;
        private const float DespawnY = CameraCenterY - CameraHalfHeight - 0.6f;
        private const float BaseFallSpeed = 3.15f;
        private const float BulletSpeed = 7.6f;
        private const float PlayerMoveSpeed = 8.2f;
        private const float PointerDragSensitivity = 1f;
        private const float GooseMuzzleForwardRatio = 0.34f;
        private const float GooseAttackVideoScale = 0.96f;
        private const float GooseAttackFireRatio = 0.46f;
        private const float GooseScale = 0.42f;
        private const float GooseRingSpacing = 0.52f;
        private const float EnemyMeleeY = PlayerY + 0.42f;
        private const float EnemyMeleeCooldown = 0.5f;
        private const int MaxGooseCount = 40;
        private const float ApproachFarScale = 0.45f;
        private const float ApproachNearScale = 1.12f;
        private const float FarLaneOffset = 0.62f;
        private const float NearLaneOffset = 1.35f;
        private const float LogicalMaxY = 14f;
        private const float ChickenAcquireTargetLogicalY = 5f;
        private const float PlayerMinLogicalX = -1.5f;
        private const float PlayerMaxLogicalX = 1.5f;
        private const float CameraMaxWorldOffsetX = 0.5f;
        private const float CameraFollowSpeed = 7.5f;
        // At logical Y=0, logical X +/-2 maps to Unity X +/-5, so one logical X unit is 2.5 Unity units.
        private const float LogicalToWorldNearScale = 2.5f;
        private const float LogicalToWorldFarScale = LogicalToWorldNearScale * FarLaneOffset / NearLaneOffset;

        private static readonly float[] LaneXs = { -1.35f, 0f, 1.35f };
        private GooseAttackFrameCache mAttackFrames;
        private Camera mCamera;
        private Transform mWorldRoot;
        private Transform mBackgroundRoot;
        private Transform mEntityRoot;
        private Transform mPlayerRoot;
        private Canvas mHudCanvas;
        private RectTransform mKillProgressFill;


        private int mKilledChickenCount;
        private int mTargetChickenCount;
        private RectTransform mSafeHud;
        private RectTransform mSafeModal;
        private CanvasScaler mHudScaler;
        private Rect mLastSafeArea;
        private Vector2Int mLastScreenSize;
        private GameObject mGameOverPanel;
        private TMP_FontAsset mHudFont;
        private TMP_Text mGameOverText;
        private TMP_Text mGameOverSubText;

        private readonly List<Entity> mEntities = new List<Entity>();
        private readonly List<GooseView> mGooseViews = new List<GooseView>();
        private readonly List<LevelPlan> mLevels = new List<LevelPlan>();
        private readonly Dictionary<int, ActorConfig> mActors = new Dictionary<int, ActorConfig>();
        private readonly Dictionary<int, MonsterConfigData> mMonsters = new Dictionary<int, MonsterConfigData>();
        private readonly Dictionary<int, BuffConfig> mBuffs = new Dictionary<int, BuffConfig>();
        private readonly HashSet<int> mBuffHistory = new HashSet<int>();
        private readonly HashSet<int> mConsumedGateGroups = new HashSet<int>();
        private readonly Dictionary<int, int> mActiveBuffByGroup = new Dictionary<int, int>();

        private Sprite mSquareSprite;
        private Sprite mCircleSprite;
        private Sprite mBackgroundSprite;
        private Sprite mGooseSlingshotSprite;
        private Sprite mGooseSlingshotLeftSprite;
        private Sprite mGooseSlingshotRightSprite;
        private Sprite mGooseBowSprite;
        private Sprite mGooseBowLeftSprite;
        private Sprite mGooseBowRightSprite;
        private Sprite mGooseStaffSprite;
        private Sprite mGooseStaffLeftSprite;
        private Sprite mGooseStaffRightSprite;
        private VideoClip mGooseSlingshotDeathClip;
        private VideoClip mGooseBowDeathClip;
        private VideoClip mGooseStaffDeathClip;
        private VideoClip mGooseSlingshotAttackClip;
        private VideoClip mGooseBowAttackClip;
        private VideoClip mGooseStaffAttackClip;
        private Shader mGooseDeathChromaKeyShader;
        private Sprite mChickenSprite;
        private Sprite mChickenBadgeSprite;
        private Sprite mChickenLeftSprite;
        private Sprite mChickenRightSprite;
        private Sprite mFatChickenSprite;
        private VideoClip mChickenWalkClip;
        private Sprite mNormalGateSprite;
        private Sprite mMultiplierGateSprite;
        private Sprite mFireGateSprite;
        private Sprite mLightningGateSprite;
        private Sprite mIceGateSprite;
        private Sprite mGooseCageSprite;
        private Sprite mBowRackSprite;
        private Sprite mStaffRackSprite;
        private Sprite mSlingshotProjectileSprite;
        private Sprite mBowProjectileSprite;
        private Sprite mStaffProjectileSprite;

        private int mLevelIndex;
        private int mEventIndex;
        private int mGooseCount;
        private int mFireLevel;
        private int mLightningLevel;
        private int mIceLevel;
        private int mActiveWeaponBuffId = 1;
        private int mActiveElementBuffId;
        private float mElapsed;
        private float mScore;
        private float mTargetX;
        private float mMoveDirection;
        private float mLastPointerScreenX;
        private bool mPointerDragging;
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
            if (mAttackFrames != null) mAttackFrames.Dispose();
            if (mHudFont != null)
            {
                foreach (Texture2D atlas in mHudFont.atlasTextures)
                    if (atlas != null) Destroy(atlas);
                Destroy(mHudFont.material);
                Destroy(mHudFont);
            }
            DestroyGeneratedSprite(ref mSquareSprite);
            DestroyGeneratedSprite(ref mCircleSprite);
            if (mChickenBadgeSprite != null) Destroy(mChickenBadgeSprite);
        }

        private void Update()
        {
            if (!mBooted)
            {
                return;
            }

            UpdateHudLayout();
            HandleInput();
            if (!mGameOver)
            {
                mElapsed += Time.deltaTime;
                UpdatePlayer();
                UpdateGooseAttackAnimations();
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

            UpdateCameraFollow();
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
            BuildModelTables();
            BuildLevels();
            LoadLevelsFromJson();
            SetupWorld();
            SetupHud();
            RestartRun();
        }

        private void SetupCamera()
        {
            GameObject cameraGo = new GameObject("DemoCamera");
            cameraGo.tag = "MainCamera";
            // Place the first goose at the lower quarter while keeping it horizontally centered.
            cameraGo.transform.position = new Vector3(0f, CameraCenterY, -10f);
            mCamera = cameraGo.AddComponent<Camera>();
            mCamera.orthographic = true;
            mCamera.orthographicSize = CameraHalfHeight;
            mCamera.clearFlags = CameraClearFlags.SolidColor;
            mCamera.backgroundColor = new Color(0.68f, 0.92f, 1f);
            cameraGo.AddComponent<AudioListener>();
        }

        private void SetupSprites()
        {
            mSquareSprite = CreateSprite(CreateTexture(64, 64, t => FillTexture(t, Color.white)), 32f);
            mCircleSprite = CreateSprite(CreateTexture(64, 64, DrawCircleTexture), 32f);
            // Keep the supplied 768x1365 artwork at its native aspect ratio.
            mBackgroundSprite = LoadArtSprite("farm_road_background", 1365f / 13f) ?? LoadArtSprite("goose_farm_background", 1376f / 13f);
            mGooseSlingshotLeftSprite = LoadGoosePoseSprite("sheet_04", -1);
            mGooseSlingshotSprite = LoadGoosePoseSprite("sheet_04", 0);
            mGooseSlingshotRightSprite = LoadGoosePoseSprite("sheet_04", 1);
            mGooseBowLeftSprite = LoadGoosePoseSprite("sheet_10", -1);
            mGooseBowSprite = LoadGoosePoseSprite("sheet_10", 0);
            mGooseBowRightSprite = LoadGoosePoseSprite("sheet_10", 1);
            mGooseStaffLeftSprite = LoadGoosePoseSprite("sheet_14", -1);
            mGooseStaffSprite = LoadGoosePoseSprite("sheet_14", 0);
            mGooseStaffRightSprite = LoadGoosePoseSprite("sheet_14", 1);
            mGooseSlingshotDeathClip = Resources.Load<VideoClip>("Art/goose_death_slingshot");
            mGooseBowDeathClip = Resources.Load<VideoClip>("Art/goose_death_bow");
            mGooseStaffDeathClip = Resources.Load<VideoClip>("Art/goose_death_staff");
            mGooseSlingshotAttackClip = Resources.Load<VideoClip>("Art/goose_attack_slingshot");
            mGooseBowAttackClip = Resources.Load<VideoClip>("Art/goose_attack_bow");
            mGooseStaffAttackClip = Resources.Load<VideoClip>("Art/goose_attack_staff");
            mGooseDeathChromaKeyShader = Resources.Load<Shader>("Shaders/GooseDeathChromaKey") ?? Shader.Find("Tuyoo/GooseDeathChromaKey");
            mNormalGateSprite = LoadArtSprite("sheet_25", 290f);
            mMultiplierGateSprite = LoadArtSprite("multiplier_gate", 520f) ?? mNormalGateSprite;
            mFireGateSprite = LoadArtSprite("element_gate_fire", 520f) ?? LoadArtSprite("sheet_31", 310f);
            mLightningGateSprite = LoadArtSprite("element_gate_lightning", 520f) ?? LoadArtSprite("sheet_34", 310f);
            mIceGateSprite = LoadArtSprite("element_gate_ice", 520f) ?? LoadArtSprite("sheet_37", 310f);
            mGooseCageSprite = LoadArtSprite("goose_cage", 520f) ?? LoadArtSprite("sheet_27", 360f);
            mBowRackSprite = LoadArtSprite("weapon_rack_bow", 520f) ?? LoadArtSprite("sheet_49", 330f);
            mStaffRackSprite = LoadArtSprite("weapon_rack_staff", 520f) ?? LoadArtSprite("sheet_52", 330f);
            mSlingshotProjectileSprite = LoadArtSprite("projectile_stone", 2400f);
            mBowProjectileSprite = LoadArtSprite("projectile_arrow", 2400f);
            mStaffProjectileSprite = LoadArtSprite("projectile_orb", 2400f);
            mChickenSprite = LoadArtSprite("sheet_73", new Rect(50f, 480f, 620f, 1180f), 260f);
            mChickenLeftSprite = LoadArtSprite("sheet_73", new Rect(730f, 450f, 560f, 1160f), 260f);
            mChickenRightSprite = mChickenLeftSprite;
            mFatChickenSprite = mChickenSprite;
            mChickenWalkClip = Resources.Load<VideoClip>("Art/chicken_walk_forward");

            if (mGooseSlingshotSprite == null)
            {
                mGooseSlingshotSprite = CreateSprite(CreateTexture(96, 96, DrawGooseTexture), 32f);
                mGooseSlingshotLeftSprite = mGooseSlingshotSprite;
                mGooseSlingshotRightSprite = mGooseSlingshotSprite;
                mGooseBowSprite = mGooseSlingshotSprite;
                mGooseBowLeftSprite = mGooseSlingshotSprite;
                mGooseBowRightSprite = mGooseSlingshotSprite;
                mGooseStaffSprite = mGooseSlingshotSprite;
                mGooseStaffLeftSprite = mGooseSlingshotSprite;
                mGooseStaffRightSprite = mGooseSlingshotSprite;
            }
            if (mChickenSprite == null)
            {
                mChickenSprite = CreateSprite(CreateTexture(96, 96, DrawChickenTexture), 32f);
                mChickenLeftSprite = mChickenSprite;
                mChickenRightSprite = mChickenSprite;
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

        private Sprite LoadArtSprite(string name, Rect rect, float pixelsPerUnit)
        {
            Texture2D texture = Resources.Load<Texture2D>("Art/" + name);
            if (texture == null)
            {
                return null;
            }
            return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        private void ScaleBackgroundToCoverCamera(GameObject background)
        {
            if (background == null || mCamera == null)
            {
                return;
            }

            SpriteRenderer renderer = background.GetComponent<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null)
            {
                return;
            }

            float cameraHeight = mCamera.orthographicSize * 2f;
            float cameraWidth = cameraHeight * mCamera.aspect + CameraMaxWorldOffsetX * 2f;
            Vector2 spriteSize = renderer.sprite.bounds.size;
            float scale = Mathf.Max(cameraWidth / spriteSize.x, cameraHeight / spriteSize.y);
            background.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private Sprite LoadGoosePoseSprite(string name, int direction)
        {
            if (direction < 0)
            {
                return LoadArtSprite(name, new Rect(70f, 0f, 590f, 980f), 340f);
            }
            if (direction > 0)
            {
                return LoadArtSprite(name, new Rect(1390f, 0f, 590f, 980f), 340f);
            }
            return LoadArtSprite(name, new Rect(680f, 0f, 690f, 980f), 340f);
        }

        private void BuildModelTables()
        {
            mActors.Clear();
            mMonsters.Clear();
            AddActor(1, "弹弓鹅", 1, 1, 100, "hpMax", 1, "atk", 1, "atkspd", 1);
            AddActor(2, "弓箭鹅", 2, 2, 100, "hpMax", 2, "atk", 2, "atkspd", 2);
            AddActor(3, "法师鹅", 3, 3, 100, "hpMax", 3, "atk", 3, "atkspd", 3);
            AddActor(4, "普通鸡", 4, 1, 100, "hpMax", 1, "atk", 1, "atkspd", 1, "spd", 6000);
            AddActor(5, "长矛鸡", 5, 2, 100, "hpMax", 2, "atk", 2, "atkspd", 2, "spd", 6000);
            AddActor(6, "剑盾鸡", 6, 3, 100, "hpMax", 3, "atk", 3, "atkspd", 3, "spd", 6000);
            AddActor(7, "武器架子1", 7, 7, 100, "hpMax", 1, "atk", 1, "def", 1, "spd", 6000);
            AddActor(8, "武器架子2", 8, 8, 100, "hpMax", 2, "atk", 2, "def", 2, "spd", 6000);
            AddActor(9, "鹅笼", 9, 9, 100, "hpMax", 3, "atk", 3, "def", 3, "spd", 6000);
            AddActor(10, "倍增门", 10, 10, 120, "spd", 6000);
            AddActor(11, "火焰门", 11, 11, 130, "spd", 6000);
            AddActor(12, "雷门", 12, 12, 140, "spd", 6000);

            mBuffs.Clear();
            AddBuff(1, "弹弓", 1, 1, 1, 0, 200, 200, 10, 10);
            AddBuff(2, "弓箭", 2, 2, 1, 0, 300, 300, 10, 10);
            AddBuff(3, "法杖", 3, 3, 1, 0, 400, 400, 10, 50);
            AddBuff(11, "火焰门", 11, 1, 2, 0, 0, 0, 0, 0);
            AddBuff(12, "雷门", 12, 1, 2, 0, 0, 0, 0, 0);
            AddBuff(110, "雷火弹", 110, 2, 2, 11, 0, 0, 0, 0);
            AddBuff(111, "雷火弹", 110, 2, 2, 12, 0, 0, 0, 0);
            AddDefaultMonster(10006, 9, "Goose Cage", "3|3|3");
            LoadModelTablesFromJson();
        }

        private void AddDefaultMonster(int id, int actorId, string note, string blood)
        {
            mMonsters[id] = new MonsterConfigData { id = id, actorId = actorId, note = note, count = 1, blood = blood };
        }

        private void AddActor(int id, string name, int buffId, int modelId, float breakValue, params object[] stats)
        {
            ActorConfig actor = new ActorConfig { Id = id, Name = name, BuffId = buffId, ModelId = modelId, BreakRadius = ModelBreakToWorld(breakValue) };
            for (int i = 0; i + 1 < stats.Length; i += 2)
            {
                actor.Stats[stats[i].ToString()] = ConvertStatValue(stats[i].ToString(), stats[i + 1]);
            }
            mActors[id] = actor;
        }

        private void AddBuff(int id, string name, int effectId, int priority, int priorityGroup, int frontBuffId, float bulletDistance, float bulletSpeed, float breakValue, float battleValue)
        {
            mBuffs[id] = new BuffConfig
            {
                Id = id,
                Name = name,
                EffectId = effectId,
                Priority = priority,
                PriorityGroup = priorityGroup,
                FrontBuffId = frontBuffId,
                BulletDistance = bulletDistance * 0.04f,
                BulletSpeed = bulletSpeed * 0.04f,
                BreakRadius = Mathf.Max(0.08f, breakValue * 0.01f),
                BattleRadius = Mathf.Max(0.08f, battleValue * 0.01f),
            };
        }

        private float ConvertStatValue(string key, object value)
        {
            float number;
            if (!float.TryParse(value.ToString(), out number))
            {
                return 0f;
            }
            return key == "spd" ? number * 0.001f : number;
        }

        private float ModelBreakToWorld(float value)
        {
            return Mathf.Max(0.1f, value * 0.001f);
        }

        private ActorConfig Actor(int id)
        {
            ActorConfig actor;
            return mActors.TryGetValue(id, out actor) ? actor : null;
        }

        private BuffConfig Buff(int id)
        {
            BuffConfig buff;
            return mBuffs.TryGetValue(id, out buff) ? buff : null;
        }

        private float ActorMoveSpeed(int actorId, float fallback)
        {
            return Mathf.Max(0.01f, ActorStat(actorId, "spd", fallback));
        }
        private float ActorStat(int actorId, string key, float fallback)
        {
            ActorConfig actor = Actor(actorId);
            float value;
            return actor != null && actor.Stats.TryGetValue(key, out value) ? value : fallback;
        }

        private MonsterConfigData GooseCageMonster()
        {
            MonsterConfigData monster;
            if (mMonsters.TryGetValue(10006, out monster))
            {
                return monster;
            }

            foreach (KeyValuePair<int, MonsterConfigData> pair in mMonsters)
            {
                if (pair.Value != null && pair.Value.actorId == 9)
                {
                    return pair.Value;
                }
            }

            return new MonsterConfigData { id = 10006, actorId = 9, count = 1, blood = "3|3|3" };
        }

        private int GooseCageBloodValue(MonsterConfigData monster, int index, int fallback)
        {
            if (monster == null || string.IsNullOrEmpty(monster.blood))
            {
                return fallback;
            }

            string[] parts = monster.blood.Split('|');
            int partIndex = parts.Length >= 3 ? index : Mathf.Min(index, parts.Length - 1);
            int value;
            return partIndex >= 0 && partIndex < parts.Length && int.TryParse(parts[partIndex], out value) ? value : fallback;
        }

        private void LoadModelTablesFromJson()
        {
            TextAsset asset = Resources.Load<TextAsset>("Config/model_config");
            if (asset == null || string.IsNullOrEmpty(asset.text))
            {
                return;
            }

            ModelConfigData data = JsonUtility.FromJson<ModelConfigData>(asset.text);
            if (data == null)
            {
                return;
            }

            if (data.actors != null && data.actors.Length > 0)
            {
                mActors.Clear();
                for (int i = 0; i < data.actors.Length; i++)
                {
                    ActorConfigData source = data.actors[i];
                    ActorConfig actor = new ActorConfig
                    {
                        Id = source.id,
                        Name = source.name,
                        BuffId = source.buffId,
                        ModelId = source.modelId,
                        BreakRadius = ModelBreakToWorld(source.breakValue),
                    };
                    if (source.stats != null)
                    {
                        for (int statIndex = 0; statIndex < source.stats.Length; statIndex++)
                        {
                            StatConfigData stat = source.stats[statIndex];
                            if (!string.IsNullOrEmpty(stat.key))
                            {
                                actor.Stats[stat.key] = ConvertStatValue(stat.key, stat.value);
                            }
                        }
                    }
                    mActors[actor.Id] = actor;
                }
            }

            if (data.buffs != null && data.buffs.Length > 0)
            {
                mBuffs.Clear();
                for (int i = 0; i < data.buffs.Length; i++)
                {
                    BuffConfigData source = data.buffs[i];
                    AddBuff(source.id, source.name, source.effectId, source.priority, source.priorityGroup, source.frontBuffId, source.bulletDistance, source.bulletSpeed, source.breakValue, source.battleValue);
                }
            }

            if (data.monsters != null && data.monsters.Length > 0)
            {
                for (int i = 0; i < data.monsters.Length; i++)
                {
                    MonsterConfigData source = data.monsters[i];
                    if (source != null && source.actorId > 0)
                    {
                        mMonsters[source.id] = source;
                    }
                }
            }
        }

        private void LoadLevelsFromJson()
        {
            TextAsset asset = Resources.Load<TextAsset>("Config/level_config");
            if (asset == null || string.IsNullOrEmpty(asset.text))
            {
                return;
            }

            LevelConfigData data;
            try
            {
                data = JsonUtility.FromJson<LevelConfigData>(asset.text);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Level config parse failed; using built-in levels: " + ex.Message);
                return;
            }
            if (data == null || data.levels == null || data.levels.Length == 0)
            {
                Debug.LogWarning("Level config is empty; using built-in levels");
                return;
            }

            List<LevelPlan> imported = new List<LevelPlan>();
            for (int levelIndex = 0; levelIndex < data.levels.Length; levelIndex++)
            {
                LevelData source = data.levels[levelIndex];
                if (source == null || source.stageId <= 0)
                {
                    continue;
                }
                LevelPlan level = new LevelPlan
                {
                    Name = string.IsNullOrEmpty(source.name) ? "关卡 " + source.stageId : source.name,
                    Tip = string.IsNullOrEmpty(source.tip) ? "" : source.tip,
                };
                if (source.events != null)
                {
                    for (int eventIndex = 0; eventIndex < source.events.Length; eventIndex++)
                    {
                        LevelEventData evt = source.events[eventIndex];
                        LevelEvent converted;
                        if (TryConvertLevelEvent(evt, out converted))
                        {
                            level.Events.Add(converted);
                        }
                    }
                }
                level.Events.Sort((a, b) => a.Time.CompareTo(b.Time));
                imported.Add(level);
            }
            if (imported.Count > 0)
            {
                mLevels.Clear();
                mLevels.AddRange(imported);
                Debug.Log("Loaded imported levels: " + imported.Count);
            }
        }

        private bool TryConvertLevelEvent(LevelEventData source, out LevelEvent converted)
        {
            converted = null;
            if (source == null || source.lane < -1 || source.lane > 1 || source.time < 0f)
            {
                return false;
            }
            if (source.kind == 0)
            {
                converted = G(source.time, source.lane, source.value);
                converted.LogicalY = source.y;
                converted.GateGroup = source.y > 0f ? Mathf.RoundToInt(source.y * 1000f) : 0;
                return true;
            }
            if (source.kind == 1 && source.element >= 1 && source.element <= 3)
            {
                converted = E(source.time, source.lane, (ElementKind)source.element);
                converted.Health = source.health;
                converted.LogicalY = source.y;
                return true;
            }
            if (source.kind == 2 && source.weapon >= 1 && source.weapon <= 2)
            {
                converted = R(source.time, source.lane, source.weapon == 1 ? WeaponKind.Bow : WeaponKind.Staff);
                converted.Health = source.health;
                converted.LogicalY = source.y;
                return true;
            }
            if (source.kind == 3 && source.chicken >= 0 && source.chicken <= 3 && source.count > 0)
            {
                converted = C(source.time, source.lane, (ChickenKind)source.chicken, source.count);
                converted.LogicalY = source.y;
                return true;
            }
            if (source.kind == 4 && source.health > 0 && source.count > 0)
            {
                converted = new LevelEvent
                {
                    Time = source.time,
                    Kind = EventKind.GooseCage,
                    Lane = source.lane,
                    Health = source.health,
                    Count = source.count,
                    LogicalY = source.y,
                };
                return true;
            }
            return false;
        }

        private void BuildLevels()
        {
            mLevels.Clear();
            mLevels.Add(Level("第1关：先打门", "开枪打 + 门会把数字打大，鹅身体穿过去才加人。",
                C(1f, 0, ChickenKind.Normal, 3), G(3f, -1, 4), G(3f, 1, 8), C(6f, 0, ChickenKind.Normal, 4),
                G(8f, 0, 6), G(11f, 0, 12), G(12f, -1, 14), G(12f, 1, 10), G(13.3f, 0, 16),
                G(16f, 0, 10), C(17f, -1, ChickenKind.Normal, 4), C(17f, 0, ChickenKind.Normal, 6), C(17f, 1, ChickenKind.Normal, 4), C(23f, 0, ChickenKind.Normal, 8)));
            mLevels.Add(Level("第2关：武器架", "木桶已替换成武器架。打爆武器架才换弓或法杖。",
                G(0.5f, 0, 8), C(3f, 0, ChickenKind.Normal, 5), R(4.5f, -1, WeaponKind.Bow), G(4.5f, 1, 12),
                G(8f, 0, 10), R(11f, 0, WeaponKind.Bow), G(12f, 0, 14), R(13.3f, 0, WeaponKind.Staff),
                G(15f, -1, 16), G(15f, 1, 12), G(16f, 0, 10), C(17f, -1, ChickenKind.Normal, 5), C(17f, 0, ChickenKind.Normal, 8), C(17f, 1, ChickenKind.Normal, 5), C(23f, 0, ChickenKind.Fat, 1)));
            mLevels.Add(Level("第3-1关：火门", "先打碎火门锁，再穿门获得 1 层火。火适合烧大肥鸡。",
                G(0.5f, -1, 8), G(0.5f, 1, 6), C(3f, 0, ChickenKind.Normal, 4), R(4.5f, 0, WeaponKind.Bow),
                E(7f, 0, ElementKind.Fire), C(10f, 0, ChickenKind.Fat, 1), G(11f, 0, 14), G(12f, -1, 12), R(12f, 1, WeaponKind.Staff), G(13.5f, 0, 16),
                G(16f, 0, 8), C(17f, -1, ChickenKind.Normal, 4), C(17f, 0, ChickenKind.Normal, 6), C(17f, 1, ChickenKind.Normal, 4), C(23f, 0, ChickenKind.Fat, 1)));
            mLevels.Add(Level("第3-2关：雷门", "先打碎雷门锁，再穿门获得 1 层雷。雷会跳到附近鸡身上。",
                G(0.5f, -1, 8), G(0.5f, 1, 6), C(3f, 0, ChickenKind.Normal, 4), R(4.5f, 0, WeaponKind.Bow),
                E(7f, 0, ElementKind.Lightning), C(10f, -1, ChickenKind.Normal, 3), C(10f, 0, ChickenKind.Normal, 3), C(10f, 1, ChickenKind.Normal, 3),
                G(11f, 0, 14), R(12f, -1, WeaponKind.Staff), G(12f, 1, 12), G(13.5f, 0, 16), G(16f, 0, 8), C(17f, -1, ChickenKind.Normal, 6), C(17f, 0, ChickenKind.Normal, 8), C(17f, 1, ChickenKind.Normal, 6), C(23f, 0, ChickenKind.Fat, 1)));
            mLevels.Add(Level("第4-1关：火变强", "重复穿过不同火门，火从 1 层升到 3 层。",
                G(0.5f, 0, 8), C(3f, 0, ChickenKind.Normal, 4), E(4.5f, 0, ElementKind.Fire), C(7f, 0, ChickenKind.Fat, 1), R(8.5f, 0, WeaponKind.Bow),
                E(11f, 0, ElementKind.Fire), G(12f, -1, 14), R(12f, 1, WeaponKind.Staff), E(13.5f, 0, ElementKind.Fire), G(15f, 0, 16), G(16f, 0, 8),
                C(17f, -1, ChickenKind.Normal, 4), C(17f, 0, ChickenKind.Fat, 1), C(17f, 1, ChickenKind.Normal, 4), C(23f, -1, ChickenKind.Normal, 5), C(23f, 0, ChickenKind.Fat, 1), C(23f, 1, ChickenKind.Normal, 5)));
            mLevels.Add(Level("第4-2关：超载", "火和雷同时存在后，只打超载爆炸，不叠播多套元素。",
                G(0.5f, 0, 8), C(3f, 0, ChickenKind.Normal, 4), E(4.5f, -1, ElementKind.Fire), E(4.5f, 1, ElementKind.Lightning),
                C(7f, 0, ChickenKind.Normal, 6), R(8.5f, 0, WeaponKind.Bow), E(11.5f, -1, ElementKind.Fire), E(11.5f, 1, ElementKind.Lightning),
                C(14f, -1, ChickenKind.Normal, 4), C(14f, 0, ChickenKind.Normal, 6), C(14f, 1, ChickenKind.Normal, 4), G(15f, 0, 16), G(16f, 0, 8), C(17f, -1, ChickenKind.Normal, 6), C(17f, 0, ChickenKind.Normal, 8), C(17f, 1, ChickenKind.Normal, 6), C(24f, 0, ChickenKind.Fat, 1)));
            mLevels.Add(Level("第4-3关：雷变强", "重复穿过雷门，跳电次数和范围提高。",
                G(0.5f, 0, 8), C(3f, 0, ChickenKind.Normal, 4), E(4.5f, 0, ElementKind.Lightning), C(7f, -1, ChickenKind.Normal, 3), C(7f, 0, ChickenKind.Normal, 3), C(7f, 1, ChickenKind.Normal, 3),
                R(8.5f, 0, WeaponKind.Bow), E(11f, 0, ElementKind.Lightning), R(12f, -1, WeaponKind.Staff), G(12f, 1, 14), E(13.5f, 0, ElementKind.Lightning), G(15f, 0, 16), G(16f, 0, 8), C(17f, -1, ChickenKind.Normal, 6), C(17f, 0, ChickenKind.Normal, 8), C(17f, 1, ChickenKind.Normal, 6), C(24f, 0, ChickenKind.Fat, 1)));
            mLevels.Add(Level("第5关：正式关卡", "开放冰、蒸发、霜雷碎、激光、减人数门、快鸡和 Boss。",
                G(0.5f, -1, 10), G(0.5f, 1, 6), C(3f, -1, ChickenKind.Normal, 3), C(3f, 0, ChickenKind.Fat, 1), C(3f, 1, ChickenKind.Fast, 1),
                E(4.8f, -1, ElementKind.Fire), E(4.8f, 0, ElementKind.Lightning), E(4.8f, 1, ElementKind.Ice), C(7f, -1, ChickenKind.Normal, 4), C(7f, 0, ChickenKind.Fat, 1), C(7f, 1, ChickenKind.Fast, 1),
                G(8f, 0, 12), G(9f, -1, 14), G(9f, 1, 10), R(10.5f, 0, WeaponKind.Bow), E(12f, -1, ElementKind.Fire), G(12f, 0, 16), E(12f, 1, ElementKind.Lightning),
                C(13f, 0, ChickenKind.Normal, 6), R(14f, 0, WeaponKind.Staff), E(15f, -1, ElementKind.Ice), E(15f, 1, ElementKind.Fire), G(16f, -1, 18), G(16f, 1, -8),
                G(17f, 0, 12), E(18f, -1, ElementKind.Lightning), E(18f, 0, ElementKind.Ice), E(18f, 1, ElementKind.Fire), C(19f, -1, ChickenKind.Normal, 6), C(19f, 0, ChickenKind.Normal, 8), C(19f, 1, ChickenKind.Normal, 6),
                C(24f, -1, ChickenKind.Fast, 1), C(24f, 0, ChickenKind.Fat, 1), C(24f, 1, ChickenKind.Fast, 1), C(27f, 0, ChickenKind.Boss, 1)));
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
            mBackgroundRoot.position = new Vector3(0f, mCamera.transform.position.y, 0f);
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
                GameObject background = CreateSpriteObject("PastoralRoad", mBackgroundRoot, mBackgroundSprite, Color.white, Vector3.zero, Vector3.one, -120);
                ScaleBackgroundToCoverCamera(background);
            }
            else
            {
                CreateSpriteObject("Sky", mBackgroundRoot, mSquareSprite, new Color(0.70f, 0.92f, 1f), new Vector3(0f, 1f, 0f), new Vector3(8f, 13f, 1f), -120);
                CreateSpriteObject("Path", mBackgroundRoot, mSquareSprite, new Color(0.94f, 0.82f, 0.55f), new Vector3(0f, -1.5f, 0f), new Vector3(4.1f, 11f, 1f), -100);
            }
        }

        private void SetupHud()
        {
            Font sourceFont = Resources.Load<Font>("Fonts/ZCOOLKuaiLe-Regular");
            mHudFont = TMP_FontAsset.CreateFontAsset(sourceFont, 128, 12,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048,
                AtlasPopulationMode.Dynamic, true);
            mHudFont.name = "ZCOOL KuaiLe HUD SDF";
            string missingCharacters;
            if (!mHudFont.TryAddCharacters("清场胜利本关失败下一关会重置为 1 只鹅、弹弓、无元素继续有鸡越过鹅群，大鹅全部倒下", out missingCharacters))
                Debug.LogWarning("HUD font missing characters: " + missingCharacters);
            // Preserve curved strokes and gently widen the SDF antialiasing transition.
            foreach (Texture2D atlas in mHudFont.atlasTextures)
            {
                atlas.filterMode = FilterMode.Bilinear;
                atlas.wrapMode = TextureWrapMode.Clamp;
            }
            mHudFont.material.SetFloat("_Sharpness", -0.15f);
            mHudFont.material.SetFloat("_OutlineWidth", 0f);
            mHudFont.material.SetFloat("_OutlineSoftness", 0.015f);
            GameObject canvasGo = new GameObject("HUD");
            canvasGo.AddComponent<RectTransform>();
            mHudCanvas = canvasGo.AddComponent<Canvas>();
            canvasGo.AddComponent<GraphicRaycaster>();
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            mHudScaler = scaler;
            mHudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mHudCanvas.sortingOrder = 200;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            EnsureEventSystem();
            mSafeHud = CreateSafeRoot("SafeHUD", mHudCanvas.transform);

            // Layer native UGUI images using the existing cream/green game palette.
            RectTransform progress = CreateSafeRoot("KillProgress", mSafeHud);
            SetHudRect(progress, new Vector2(0.25f, 1f), new Vector2(0.75f, 1f), new Vector2(0f, -64f), new Vector2(0f, -28f));
            CreateProgressImage("Shadow", progress, new Color(0.12f, 0.16f, 0.10f, 0.3f), Vector2.zero, Vector2.one, new Vector2(0f, -4f), new Vector2(0f, -4f));
            CreateProgressImage("Outline", progress, new Color(0.24f, 0.29f, 0.16f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CreateProgressImage("CreamFrame", progress, new Color(0.98f, 0.93f, 0.72f), Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            CreateProgressImage("FrameHighlight", progress, new Color(1f, 0.99f, 0.9f), new Vector2(0f, 1f), Vector2.one, new Vector2(3f, -4f), new Vector2(-3f, -2f));
            RectTransform inner = CreateProgressImage("Track", progress, new Color(0.20f, 0.28f, 0.17f), Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            mKillProgressFill = CreateProgressImage("Fill", inner, new Color(0.38f, 0.73f, 0.27f), Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);

            mKillProgressFill.gameObject.AddComponent<KillProgressGradient>();
            CreateProgressImage("FillHighlight", mKillProgressFill, new Color(0.86f, 1f, 0.95f, 0.18f), new Vector2(0f, 0.82f), Vector2.one, Vector2.zero, Vector2.zero);
            CreateProgressImage("FillShade", mKillProgressFill, new Color(0.05f, 0.24f, 0.18f, 0.18f), Vector2.zero, new Vector2(1f, 0.18f), Vector2.zero, Vector2.zero);
            for (int i = 1; i < 10; i++)
            {
                float x = i / 10f;
                CreateProgressImage("Tick" + i, inner, new Color(0.95f, 1f, 0.83f, 0.28f), new Vector2(x, 0f), new Vector2(x, 0.25f), new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f));
            }

            // Reuse the front-facing enemy artwork as a readable chicken portrait.
            RectTransform badge = CreateProgressImage("ChickenBadge", progress, new Color(0.24f, 0.29f, 0.16f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-21f, -28f), new Vector2(35f, 28f));
            badge.GetComponent<Image>().sprite = mCircleSprite;
            RectTransform badgeFace = CreateProgressImage("BadgeFace", badge, new Color(0.98f, 0.93f, 0.72f), Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            badgeFace.GetComponent<Image>().sprite = mCircleSprite;
            mChickenBadgeSprite = LoadArtSprite("sheet_73", new Rect(300f, 1080f, 260f, 420f), 260f);
            RectTransform portrait = CreateProgressImage("ChickenPortrait", badgeFace, Color.white, Vector2.zero, Vector2.one, new Vector2(1f, 0f), new Vector2(-1f, 0f));
            Image portraitImage = portrait.GetComponent<Image>();
            portraitImage.sprite = mChickenBadgeSprite != null ? mChickenBadgeSprite : mChickenSprite;
            portraitImage.preserveAspect = true;
            CreateGameOverHud();
            UpdateHudLayout();
        }

        private RectTransform CreateProgressImage(string name, Transform parent, Color color, Vector2 min, Vector2 max, Vector2 insetMin, Vector2 insetMax)
        {
            RectTransform rect = CreateSafeRoot(name, parent);
            SetHudRect(rect, min, max, insetMin, insetMax);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private RectTransform CreateSafeRoot(string name, Transform parent)
        {
            RectTransform root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            return root;
        }

        private static void SetHudRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 insetMin, Vector2 insetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = insetMin;
            rect.offsetMax = insetMax;
        }

        private void UpdateHudLayout()
        {
            if (mSafeHud == null || Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = Screen.safeArea;
            if (safe.width <= 0f || safe.height <= 0f) safe = new Rect(0f, 0f, Screen.width, Screen.height);
            Vector2Int size = new Vector2Int(Screen.width, Screen.height);
            if (size == mLastScreenSize && safe == mLastSafeArea) return;
            mLastScreenSize = size;
            mLastSafeArea = safe;
            // Scale against the usable area, including landscape and devices with cutouts.
            mHudScaler.referenceResolution = new Vector2(720f * Screen.width / safe.width, 720f * Screen.height / safe.height);
            Vector2 min = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            Vector2 max = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            SetHudRect(mSafeHud, min, max, Vector2.zero, Vector2.zero);
            SetHudRect(mSafeModal, min, max, Vector2.zero, Vector2.zero);
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

            mSafeModal = CreateSafeRoot("SafeModal", mGameOverPanel.transform);
            GameObject card = new GameObject("CenterCard");
            card.AddComponent<RectTransform>();
            card.transform.SetParent(mSafeModal, false);
            card.AddComponent<Image>().color = new Color(0.98f, 0.95f, 0.87f, 0.95f);
            RectTransform cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(560f, 300f);

            mGameOverText = CreateHudText("GameOverText", card.transform, new Vector2(0f, 48f), TextAnchor.MiddleCenter, 38, new Color(0.18f, 0.13f, 0.09f));
            mGameOverSubText = CreateHudText("GameOverSubText", card.transform, new Vector2(0f, -8f), TextAnchor.MiddleCenter, 23, new Color(0.23f, 0.19f, 0.14f));
            SetHudRect(mGameOverText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -84f), new Vector2(-24f, -20f));
            SetHudRect(mGameOverSubText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -172f), new Vector2(-24f, -92f));
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
            foreach (GooseView view in mGooseViews)
            {
                RemoveGooseAttackEffect(view);
                view.AttackPlaying = false;
                view.AttackBulletFired = false;
                view.AttackTimer = 0f;
            }
            mGooseCount = 1;
            mFireLevel = 0;
            mLightningLevel = 0;
            mIceLevel = 0;
            mWeapon = WeaponKind.Slingshot;
            mActiveWeaponBuffId = 1;
            mActiveElementBuffId = 0;
            mBuffHistory.Clear();
            mActiveBuffByGroup.Clear();
            mConsumedGateGroups.Clear();
            GrantBuff(1, false);
            EnsureGooseView(0);
            ResetGooseHealth(mGooseViews[0]);
            mEventIndex = 0;
            mElapsed = 0f;
            mScore = 0f;
            mKilledChickenCount = 0;
            mTargetChickenCount = 0;
            foreach (LevelEvent evt in mLevels[mLevelIndex].Events)
            {
                if (evt.Kind == EventKind.ChickenGroup)
                {
                    mTargetChickenCount += Mathf.Max(0, evt.Count);
                }
            }
            RefreshHud();
            mTargetX = 0f;
            mMoveDirection = 0f;
            mPointerDragging = false;
            mLastPointerScreenX = 0f;
            mGameOver = false;
            mVictory = false;
            mPlayerRoot.position = new Vector3(0f, PlayerY, 0f);
            SetCameraX(0f);
            RefreshGooseFormation();
            mGameOverPanel.SetActive(false);
        }

        private void ClearEntities()
        {
            for (int i = 0; i < mEntities.Count; i++)
            {
                if (mEntities[i].Root != null)
                {
                    DestroyEntity(mEntities[i]);
                }
            }
            mEntities.Clear();
        }

        private void UpdatePlayer()
        {
            float currentX = mPlayerRoot.position.x;
            float nextX = Mathf.MoveTowards(currentX, mTargetX, PlayerMoveSpeed * Time.deltaTime);
            float deltaX = nextX - currentX;
            mMoveDirection = Mathf.Abs(deltaX) > 0.001f ? Mathf.Sign(deltaX) : 0f;
            mPlayerRoot.position = new Vector3(nextX, PlayerY, 0f);
        }

        private void UpdateCameraFollow()
        {
            if (mCamera == null || mPlayerRoot == null)
            {
                return;
            }

            float targetX = Mathf.Clamp(mPlayerRoot.position.x, -CameraMaxWorldOffsetX, CameraMaxWorldOffsetX);
            float nextX = Mathf.MoveTowards(mCamera.transform.position.x, targetX, CameraFollowSpeed * Time.deltaTime);
            SetCameraX(nextX);
        }

        private void SetCameraX(float x)
        {
            if (mCamera == null)
            {
                return;
            }

            mCamera.transform.position = new Vector3(x, CameraCenterY, -10f);
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
                float cooldown = GetWeaponCooldown();
                if (!BeginGooseAttack(view, cooldown))
                {
                    FireBullet(GetGooseMuzzlePosition(view));
                }
                view.ShootTimer = cooldown;
            }
        }

        private void UpdateGooseAttackAnimations()
        {
            VideoClip clip = GooseAttackClipForWeapon();
            if (clip != null && mGooseDeathChromaKeyShader != null && (mAttackFrames == null || mAttackFrames.Clip != clip))
            {
                foreach (GooseView goose in mGooseViews) RemoveGooseAttackEffect(goose);
                if (mAttackFrames != null) mAttackFrames.Dispose();
                mAttackFrames = new GooseAttackFrameCache(transform, clip, mGooseDeathChromaKeyShader);
            }
            for (int i = 0; i < mGooseViews.Count; i++)
            {
                GooseView view = mGooseViews[i];
                if (view == null || !view.AttackPlaying)
                {
                    continue;
                }

                if (i >= mGooseCount || view.Root == null || !view.Root.activeSelf)
                {
                    RemoveGooseAttackEffect(view);
                    view.AttackPlaying = false;
                    view.AttackBulletFired = false;
                    view.AttackTimer = 0f;
                    if (view.Renderer != null)
                    {
                        view.Renderer.enabled = true;
                    }
                    continue;
                }

                view.AttackTimer += Time.deltaTime;
                if (!view.AttackBulletFired && view.AttackTimer >= view.AttackFireTime)
                {
                    view.AttackBulletFired = true;
                    FireBullet(GetGooseMuzzlePosition(view));
                }

                if (view.AttackTimer >= view.AttackDuration)
                {
                    RemoveGooseAttackEffect(view);
                    view.AttackPlaying = false;
                    view.AttackTimer = 0f;
                    if (view.Renderer != null)
                    {
                        view.Renderer.enabled = true;
                    }
                }
            }
        }

        private Vector3 GetGooseMuzzlePosition(GooseView view)
        {
            if (view == null || view.Root == null)
            {
                return mPlayerRoot != null ? mPlayerRoot.position + new Vector3(0f, 0.32f, 0f) : new Vector3(0f, PlayerY + 0.32f, 0f);
            }

            if (view.Renderer == null || view.Renderer.sprite == null || view.Root == null)
            {
                return view.Root.transform.position + new Vector3(0f, 0.32f, 0f);
            }

            Bounds bounds = view.Renderer.sprite.bounds;
            Vector3 center = view.Root.transform.TransformPoint(bounds.center);
            float forwardOffset = bounds.extents.y * view.Root.transform.lossyScale.y * GooseMuzzleForwardRatio;
            return new Vector3(
                center.x,
                center.y + forwardOffset,
                view.Root.transform.position.z);
        }

        private bool BeginGooseAttack(GooseView view, float cooldown)
        {
            VideoClip clip = GooseAttackClipForWeapon();
            if (clip == null || view == null || view.Root == null)
            {
                return false;
            }

            float sourceDuration = GetGooseAttackSourceDuration(clip);
            float duration = Mathf.Clamp(cooldown * 0.82f, 0.055f, Mathf.Min(sourceDuration, 0.72f));
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
            view.AttackPlaying = true;
            view.AttackBulletFired = false;
            view.AttackTimer = 0f;
            view.AttackDuration = duration;
            view.AttackFireTime = duration * GooseAttackFireRatio;
            return true;
        }

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

        private float GetGooseAttackSourceDuration(VideoClip clip)
        {
            if (clip == null || clip.length <= 0.01)
            {
                return 0.3f;
            }

            return (float)clip.length * 0.5f;
        }

        private float GetGooseAttackSegmentStart(GooseView view, VideoClip clip, float sourceDuration)
        {
            if (view == null || clip == null || clip.length <= sourceDuration + 0.01)
            {
                return 0f;
            }

            int segmentCount = Mathf.Max(1, Mathf.FloorToInt((float)clip.length / Mathf.Max(0.01f, sourceDuration)));
            int segmentIndex = Mathf.Abs(view.AttackSegmentIndex++) % segmentCount;
            return Mathf.Min(segmentIndex * sourceDuration, Mathf.Max(0f, (float)clip.length - sourceDuration));
        }

        private Vector3 GetGooseAttackEffectScale(GooseView view)
        {
            if (view == null || view.Renderer == null || view.Renderer.sprite == null || view.Root == null)
            {
                return Vector3.one * 0.58f;
            }

            Bounds bounds = view.Renderer.sprite.bounds;
            Vector3 scale = view.Root.transform.lossyScale;
            float height = Mathf.Max(0.05f, bounds.size.y * Mathf.Abs(scale.y) * GooseAttackVideoScale);
            // Match the attack video width to the current front-facing goose sprite.
            // The source videos are 480x854, while the game's goose sprite frame is wider.
            float spriteAspect = bounds.size.x / Mathf.Max(0.001f, bounds.size.y);
            return new Vector3(height * spriteAspect, height, 1f);
        }

        private void FireBullet(Vector3 origin)
        {
            AttackKind attack = ChooseAttack();
            if (attack == AttackKind.Laser)
            {
                FireLaser(origin);
                return;
            }
            BuffConfig weaponBuff = Buff(mActiveWeaponBuffId);
            Entity bullet = CreateEntity(EntityKind.Bullet, "Shot_" + Time.frameCount, NearestLane(origin.x), origin.y, weaponBuff != null && weaponBuff.BulletSpeed > 0f ? weaponBuff.BulletSpeed : BulletSpeed);
            bullet.Transform.position = origin;
            UpdateLogicalPosition(bullet);
            bullet.StartY = bullet.LogicalY;
            float worldRange = weaponBuff != null && weaponBuff.BulletDistance > 0f ? weaponBuff.BulletDistance : 4f;
            bullet.Range = WorldDistanceToLogicalY(worldRange);
            bullet.BreakRadius = weaponBuff != null ? weaponBuff.BreakRadius : 0.11f;
            bullet.DamageRadius = weaponBuff != null ? weaponBuff.BattleRadius : bullet.BreakRadius;
            bullet.Body.sprite = ProjectileSpriteForWeapon();
            bullet.Body.color = ProjectileTintForAttack(attack);
            bullet.BaseScale = ProjectileScaleForWeapon();
            ApplyEntityPerspective(bullet);
            bullet.Damage = GetWeaponDamage();
            bullet.Attack = attack;
            bullet.PierceLeft = mWeapon == WeaponKind.Bow ? 1 : 0;
            mEntities.Add(bullet);
        }

        private Sprite ProjectileSpriteForWeapon()
        {
            if (mWeapon == WeaponKind.Bow)
            {
                return mBowProjectileSprite ?? mSquareSprite;
            }
            if (mWeapon == WeaponKind.Staff)
            {
                return mStaffProjectileSprite ?? mCircleSprite;
            }
            return mSlingshotProjectileSprite ?? mCircleSprite;
        }

        private Vector3 ProjectileScaleForWeapon()
        {
            if (mWeapon == WeaponKind.Bow)
            {
                return Vector3.one * 1.45f;
            }
            if (mWeapon == WeaponKind.Staff)
            {
                return Vector3.one * 1.10f;
            }
            return Vector3.one * 1.15f;
        }

        private Color ProjectileTintForAttack(AttackKind attack)
        {
            return attack == AttackKind.Normal ? Color.white : GetAttackColor(attack);
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
                SpawnGate(evt.Lane, evt.Value, evt.LogicalY, evt.GateGroup);
            }
            else if (evt.Kind == EventKind.ElementGate)
            {
                SpawnElementGate(evt.Lane, evt.Element, evt.LogicalY, evt.Health);
            }
            else if (evt.Kind == EventKind.WeaponRack)
            {
                SpawnWeaponRack(evt.Lane, evt.Weapon, evt.LogicalY, evt.Health);
            }
            else if (evt.Kind == EventKind.GooseCage)
            {
                SpawnGooseCage(evt.Lane, evt.LogicalY, evt.Health, evt.Count);
            }
            else
            {
                for (int i = 0; i < evt.Count; i++)
                {
                    float y = evt.LogicalY > 0f ? LogicalToWorldY(evt.LogicalY + i * 0.45f) : SpawnY + i * 0.32f;
                    SpawnChicken(evt.Lane, evt.Chicken, y, (i % 3 - 1) * 0.18f);
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
                    float motionScale = PerspectiveMotionScale(entity.LogicalY);
                    entity.LogicalY += WorldDistanceToLogicalY(entity.Speed * motionScale * Time.deltaTime);
                    SyncEntityPosition(entity);
                    ApplyEntityPerspective(entity);
                    TryResolveBulletHit(entity);
                    entity.Consumed |= entity.LogicalY > LogicalMaxY + WorldDistanceToLogicalY(0.6f) || entity.LogicalY - entity.StartY >= entity.Range;
                }
                else if (entity.Kind == EntityKind.Effect)
                {
                    UpdateEffect(entity);
                }
                else
                {
                    UpdateFallingEntity(entity, playerRect);
                }

                if (entity.Consumed || IsEntityBelowDespawn(entity))
                {
                    if (!mGameOver && entity.Kind == EntityKind.Chicken && !entity.Consumed)
                    {
                        SetGameOver(false, "有鸡越过鹅群，本关失败");
                    }
                    DestroyEntity(entity);
                    mEntities.RemoveAt(i);
                }
            }
        }

        private void UpdateFallingEntity(Entity entity, Rect playerRect)
        {
            if (entity.Kind == EntityKind.Chicken)
            {
                UpdateChickenEntity(entity);
                return;
            }

            float speed = entity.Kind == EntityKind.Chicken && entity.SlowTimer > 0f ? entity.Speed * 0.35f : entity.Speed;
            float motionScale = PerspectiveMotionScale(entity.LogicalY);
            entity.LogicalY -= WorldDistanceToLogicalY(speed * motionScale * Time.deltaTime);
            SyncEntityPosition(entity);
            ApplyEntityPerspective(entity);

            if (entity.Kind == EntityKind.GooseCage && PlayerOverlapsEntityRadius(entity, playerRect))
            {
                ResolveGooseCageReward(entity);
            }
            else if (entity.Kind == EntityKind.Gate && playerRect.Overlaps(GetEntityRect(entity)))
            {
                entity.Consumed = ResolveGate(entity);
            }
            else if (entity.Kind == EntityKind.ElementGate && playerRect.Overlaps(GetEntityRect(entity)))
            {
                if (entity.Unlocked)
                {
                    AddElement(entity.Element);
                    SpawnText(entity.Transform.position, ElementAcquireText(entity.Element), GetElementColor(entity.Element), 0.42f);
                    entity.Consumed = true;
                }
                else
                {
                    DamageGoose(GetAttackDamage(entity));
                    entity.Consumed = true;
                }
            }
        }

        private void UpdateChickenEntity(Entity chicken)
        {
            float speed = chicken.SlowTimer > 0f ? chicken.Speed * 0.35f : chicken.Speed;
            Vector3 current = chicken.Transform.position;
            if (chicken.LogicalY > WorldToLogicalY(EnemyMeleeY))
            {
                float motionScale = PerspectiveMotionScale(chicken.LogicalY);
                float worldStep = speed * motionScale * Time.deltaTime;
                chicken.LogicalY -= WorldDistanceToLogicalY(worldStep);
                // Stay in the spawn lane until entering the target acquisition zone.
                if (chicken.LogicalY < ChickenAcquireTargetLogicalY)
                {
                    float targetLogicalX = GetPlayerMeleeLogicalX() - chicken.LogicalOffsetX;
                    chicken.LogicalX = Mathf.MoveTowards(chicken.LogicalX, targetLogicalX, WorldDistanceToLogicalX(worldStep * 0.85f, chicken.LogicalY));
                }
                SyncEntityPosition(chicken);
                UpdateChickenVisual(chicken);
                return;
            }

            Vector3 target = new Vector3(mPlayerRoot.position.x, EnemyMeleeY, current.z);
            float meleeDistance = Mathf.Max(0.42f, chicken.BreakRadius + 0.22f);
            Vector3 offset = target - current;

            if (offset.magnitude > meleeDistance)
            {
                Vector3 next = Vector3.MoveTowards(current, target, speed * Time.deltaTime);
                chicken.Transform.position = next;
                UpdateLogicalPosition(chicken);
                UpdateChickenVisual(chicken);
                return;
            }

            chicken.Transform.position = new Vector3(current.x, Mathf.Min(current.y, EnemyMeleeY), current.z);
            UpdateLogicalPosition(chicken);
            UpdateChickenVisual(chicken);
            chicken.MeleeTimer -= Time.deltaTime;
            if (chicken.MeleeTimer <= 0f)
            {
                DamageGoose(GetAttackDamage(chicken));
                chicken.MeleeTimer = EnemyMeleeCooldown;
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
                        DestroyEntity(mEntities[i]);
                        mEntities.RemoveAt(i);
                    }
                }
            }
        }

        private void DestroyEntity(Entity entity)
        {
            if (entity == null)
            {
                return;
            }

            if (entity.Root != null)
            {
                Destroy(entity.Root);
            }
            if (entity.EffectTexture != null)
            {
                entity.EffectTexture.Release();
                Destroy(entity.EffectTexture);
                entity.EffectTexture = null;
            }
            if (entity.EffectMaterial != null)
            {
                Destroy(entity.EffectMaterial);
                entity.EffectMaterial = null;
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
            if (target.Kind == EntityKind.GooseCage)
            {
                ApplyGooseCageDamage(target, bullet.Damage);
            }
            else if (target.Kind == EntityKind.Gate)
            {
                int delta = Mathf.Max(1, bullet.Damage / 10);
                target.Amount += delta;
                UpdateGateVisual(target, true);
                mScore += 2f;
            }
            else if (target.Kind == EntityKind.ElementGate)
            {
                if (!target.Unlocked)
                {
                    target.Health -= bullet.Damage;
                    UpdateElementGateLockLabel(target);
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
                UpdateWeaponRackLabel(target);
                if (target.Health <= 0)
                {
                    GrantBuff(target.Weapon == WeaponKind.Bow ? 2 : 3, true);
                    target.Consumed = true;
                    SpawnText(target.Transform.position, WeaponName(target.Weapon), new Color(1f, 0.82f, 0.22f), 0.45f);
                }
            }
            else if (target.Kind == EntityKind.Chicken)
            {
                ApplyBulletDamageArea(target.Transform.position, Mathf.Max(bullet.BreakRadius, bullet.DamageRadius), bullet.Damage, bullet.Attack);
            }
        }

        private void ApplyBulletDamageArea(Vector3 center, float radius, int damage, AttackKind attack)
        {
            bool hitAny = false;
            for (int i = 0; i < mEntities.Count; i++)
            {
                Entity target = mEntities[i];
                if (target.Kind != EntityKind.Chicken || target.Consumed || Vector3.Distance(center, target.Transform.position) > radius)
                {
                    continue;
                }
                ApplyAttackToChicken(target, damage, attack);
                hitAny = true;
            }
            if (hitAny && radius > 0.18f)
            {
                GameObject flash = CreateSpriteObject("DamageRadius", mEntityRoot, mCircleSprite, new Color(1f, 0.82f, 0.24f, 0.22f), center, new Vector3(radius * 2f, radius * 2f, 1f), 38);
                mEntities.Add(new Entity { Kind = EntityKind.Effect, Root = flash, Transform = flash.transform, Body = flash.GetComponent<SpriteRenderer>(), Life = 0.16f });
            }
        }

        private void ApplyAttackToChicken(Entity chicken, int baseDamage, AttackKind attack)
        {
            int damage = baseDamage;
            if (attack == AttackKind.Fire)
            {
                chicken.BurnDps = Mathf.Max(chicken.BurnDps, FireBurnRate());
                damage = Mathf.RoundToInt(baseDamage * 0.55f);
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

        private int CalculateDamage(float attack, float defense)
        {
            return Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0f, attack) / Mathf.Max(1f, defense)));
        }

        private int GetAttackDamage(Entity attacker)
        {
            if (attacker == null)
            {
                return 1;
            }

            return Mathf.Max(1, attacker.Damage);
        }

        private float GetGooseDefense()
        {
            return Mathf.Max(1f, ActorStat(GooseActorIdForWeapon(), "def", 1f));
        }

        private int GetGooseMaxHealth()
        {
            return Mathf.Max(1, Mathf.RoundToInt(ActorStat(GooseActorIdForWeapon(), "hpMax", 1f)));
        }

        private void ApplyChickenDamage(Entity chicken, int damage, AttackKind attack)
        {
            if (chicken.Consumed)
            {
                return;
            }
            chicken.Health -= CalculateDamage(damage, chicken.Defense);
            UpdateEnemyHealth(chicken);
            if (chicken.Health <= 0)
            {
                chicken.Consumed = true;
                mKilledChickenCount++;
                mScore += chicken.Chicken == ChickenKind.Boss ? 200f : chicken.Chicken == ChickenKind.Fat ? 40f : 12f;
                SpawnDeathEffect(chicken, attack);
            }
        }

        private void ApplyGooseCageDamage(Entity cage, int damage)
        {
            if (cage.Consumed)
            {
                return;
            }

            cage.Health -= CalculateDamage(damage, cage.Defense);
            UpdateGooseCageLabels(cage);
            if (cage.Health <= 0)
            {
                ResolveGooseCageReward(cage);
            }
        }

        private void ResolveGooseCageReward(Entity cage)
        {
            if (cage == null || cage.Consumed)
            {
                return;
            }

            cage.Consumed = true;
            int reward = Mathf.Max(1, cage.Amount);
            int before = mGooseCount;
            AddGoose(reward);
            int added = mGooseCount - before;
            SpawnText(cage.Transform.position, "嘎嘎嘎", new Color(0.22f, 0.82f, 0.37f), 0.17f);
            mScore += added * 8f;
        }

        private void UpdateGooseCageLabels(Entity cage)
        {
            if (cage == null)
            {
                return;
            }

            SetWorldLabelText(cage, Mathf.Max(0, cage.Health).ToString());
            if (cage.RewardLabel != null)
            {
                cage.RewardLabel.text = "+" + Mathf.Max(1, cage.Amount);
            }
        }

        private void UpdateWeaponRackLabel(Entity rack)
        {
            if (rack == null)
            {
                return;
            }

            SetWorldLabelText(rack, Mathf.Max(0, rack.Health).ToString());
        }

        private void UpdateElementGateLockLabel(Entity gate)
        {
            if (gate == null)
            {
                return;
            }

            SetWorldLabelText(gate, Mathf.Max(0, gate.Health).ToString());
        }

        private bool ResolveGate(Entity gate)
        {
            if (gate.GateGroup > 0 && mConsumedGateGroups.Contains(gate.GateGroup))
            {
                return false;
            }

            if (gate.GateGroup > 0)
            {
                mConsumedGateGroups.Add(gate.GateGroup);
            }

            if (gate.Amount >= 0)
            {
                int before = mGooseCount;
                AddGoose(gate.Amount);
                int added = mGooseCount - before;
                SpawnText(gate.Transform.position, "+" + added, new Color(0.22f, 0.82f, 0.37f), 0.5f);
                mScore += added * 8f;
            }
            else
            {
                int before = mGooseCount;
                RemoveGoose(Mathf.Min(Mathf.Max(0, -gate.Amount), Mathf.Max(0, mGooseCount - 1)));
                SpawnText(gate.Transform.position, "-" + (before - mGooseCount), new Color(0.95f, 0.18f, 0.14f), 0.5f);
            }
            return true;
        }

        private void AddGoose(int amount)
        {
            int before = mGooseCount;
            mGooseCount = Mathf.Clamp(mGooseCount + Mathf.Max(0, amount), 0, MaxGooseCount);
            for (int i = before; i < mGooseCount; i++)
            {
                EnsureGooseView(i);
                ResetGooseHealth(mGooseViews[i]);
                SyncGooseShootTimerToLeader(mGooseViews[i]);
            }
            RefreshGooseFormation();
        }

        private void SyncGooseShootTimerToLeader(GooseView view)
        {
            if (view == null || mGooseViews.Count == 0 || mGooseViews[0] == null || view == mGooseViews[0])
            {
                return;
            }

            GooseView leader = mGooseViews[0];
            view.ShootTimer = leader.ShootTimer;
            view.AttackSegmentIndex = leader.AttackSegmentIndex;
        }

        private void RemoveGoose(int amount)
        {
            int removeCount = Mathf.Max(0, amount);
            for (int i = 0; i < removeCount && mGooseCount > 0; i++)
            {
                EnsureGooseView(mGooseCount - 1);
                GooseView view = mGooseViews[mGooseCount - 1];
                Vector3 deathPosition = GetGooseDeathEffectPosition(view);
                mGooseCount--;
                if (mGooseCount < mGooseViews.Count)
                {
                    mGooseViews[mGooseCount].Health = 0;
                    UpdateGooseHealthBar(mGooseViews[mGooseCount]);
                }
                SpawnGooseDeathEffect(deathPosition);
            }
            RefreshGooseFormation();
        }

        private void AddElement(ElementKind element)
        {
            if (element == ElementKind.Fire)
            {
                GrantBuff(11, true);
            }
            else if (element == ElementKind.Lightning)
            {
                GrantBuff(12, true);
            }
            else if (element == ElementKind.Ice)
            {
                mIceLevel = Mathf.Min(3, mIceLevel + 1);
            }
            RefreshGooseWeaponArt();
        }

        private void GrantBuff(int buffId, bool showText)
        {
            BuffConfig buff = Buff(buffId);
            if (buff == null)
            {
                return;
            }

            mBuffHistory.Add(buffId);
            ApplyBuffByPriority(buff);

            for (int i = 0; i < 2; i++)
            {
                foreach (BuffConfig candidate in mBuffs.Values)
                {
                    if (candidate.FrontBuffId <= 0 || candidate.FrontBuffId == buff.Id || candidate.PriorityGroup != buff.PriorityGroup || mBuffHistory.Contains(candidate.Id))
                    {
                        continue;
                    }
                    if (mBuffHistory.Contains(candidate.FrontBuffId))
                    {
                        mBuffHistory.Add(candidate.Id);
                        ApplyBuffByPriority(candidate);
                        if (showText)
                        {
                            SpawnText(mPlayerRoot.position + Vector3.up * 0.9f, candidate.Name, new Color(1f, 0.72f, 0.18f), 0.42f);
                        }
                    }
                }
            }

            RefreshActiveBuffState();
            RefreshGooseWeaponArt();
        }

        private void ApplyBuffByPriority(BuffConfig buff)
        {
            int currentId;
            if (mActiveBuffByGroup.TryGetValue(buff.PriorityGroup, out currentId))
            {
                BuffConfig current = Buff(currentId);
                if (current != null && current.Priority > buff.Priority)
                {
                    return;
                }
            }
            mActiveBuffByGroup[buff.PriorityGroup] = buff.Id;
        }

        private void RefreshActiveBuffState()
        {
            int weaponBuff;
            if (mActiveBuffByGroup.TryGetValue(1, out weaponBuff))
            {
                mActiveWeaponBuffId = weaponBuff;
                if (weaponBuff == 2)
                {
                    mWeapon = WeaponKind.Bow;
                }
                else if (weaponBuff == 3)
                {
                    mWeapon = WeaponKind.Staff;
                }
                else
                {
                    mWeapon = WeaponKind.Slingshot;
                }
            }

            int elementBuff;
            mFireLevel = 0;
            mLightningLevel = 0;
            if (mActiveBuffByGroup.TryGetValue(2, out elementBuff))
            {
                mActiveElementBuffId = elementBuff;
                mFireLevel = elementBuff == 11 || elementBuff == 110 || elementBuff == 111 ? 1 : 0;
                mLightningLevel = elementBuff == 12 || elementBuff == 110 || elementBuff == 111 ? 1 : 0;
            }
            else
            {
                mActiveElementBuffId = 0;
            }
        }

        private void DamageGoose(int attack)
        {
            if (mGameOver)
            {
                return;
            }

            if (mGooseCount <= 0)
            {
                SetGameOver(false, "大鹅全部倒下，本关失败");
                return;
            }

            int damage = CalculateDamage(attack, GetGooseDefense());
            EnsureGooseView(mGooseCount - 1);
            int targetIndex = mGooseCount - 1;
            EnsureGooseHealth(mGooseViews[targetIndex]);
            mGooseViews[targetIndex].Health -= damage;
            bool gooseDefeated = mGooseViews[targetIndex].Health <= 0;
            if (gooseDefeated)
            {
                Vector3 deathPosition = GetGooseDeathEffectPosition(mGooseViews[targetIndex]);
                mGooseViews[targetIndex].Health = 0;
                mGooseCount--;
                SpawnGooseDeathEffect(deathPosition);
            }
            UpdateGooseHealthBar(mGooseViews[targetIndex]);
            RefreshGooseFormation();
            string text = gooseDefeated ? "鹅-1" : "-" + damage + " HP";
            SpawnText(mPlayerRoot.position + Vector3.up * 0.72f, text, new Color(1f, 0.22f, 0.18f), 0.42f);
            if (mGooseCount <= 0)
            {
                SetGameOver(false, "大鹅全部倒下，本关失败");
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
            if (mGameOver) return;
            // Count every configured enemy, including waves that have not spawned yet.
            // Resolve through the existing result/continue flow without waiting for gates.
            if (mTargetChickenCount > 0)
            {
                if (mKilledChickenCount >= mTargetChickenCount)
                {
                    for (int i = 0; i < mEntities.Count; i++)
                    {
                        EntityKind kind = mEntities[i].Kind;
                        if (kind == EntityKind.Chicken || kind == EntityKind.Gate || kind == EntityKind.GooseCage || kind == EntityKind.ElementGate || kind == EntityKind.WeaponRack)
                        {
                            return;
                        }
                    }
                    RefreshHud();
                    SetGameOver(true, "下一关会重置为 1 只鹅、弹弓、无元素");
                }
                return;
            }
            // Preserve the existing completion rule for levels with no enemies.
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
                EnsureGooseView(mGooseViews.Count);
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
                EnsureGooseHealth(view);
                view.Root.transform.localPosition = GetGooseFormationPosition(i);
                view.Root.transform.localRotation = Quaternion.identity;
                view.Renderer.sprite = CurrentGooseSprite();
                view.Renderer.color = CurrentGooseTint();
                view.Renderer.sortingOrder = 20 + Mathf.Clamp(i, 0, 30);
                view.Root.transform.localScale = Vector3.one * (GooseScale + Mathf.Sin(Time.time * 5f + i * 0.35f) * 0.018f);
                RefreshGooseAttackVisual(view);
                UpdateGooseHealthBar(view);
            }
            mTargetX = Mathf.Clamp(mTargetX, GetLeftBound(), GetRightBound());
            mPlayerRoot.position = new Vector3(Mathf.Clamp(mPlayerRoot.position.x, GetLeftBound(), GetRightBound()), PlayerY, 0f);
        }

        private Vector3 GetGooseFormationPosition(int index)
        {
            if (index <= 0)
            {
                return Vector3.zero;
            }

            int ring = 1;
            int ringStart = 1;
            while (index >= ringStart + ring * 6)
            {
                ringStart += ring * 6;
                ring++;
            }

            int slot = index - ringStart;
            int ringCount = ring * 6;
            float angle = Mathf.PI * 0.5f + slot * Mathf.PI * 2f / ringCount;
            float radius = ring * GooseRingSpacing;
            return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }

        private void EnsureGooseView(int index)
        {
            while (mGooseViews.Count <= index)
            {
                mGooseViews.Add(CreateGooseView(mGooseViews.Count));
            }
        }

        private GooseView CreateGooseView(int index)
        {
            GameObject go = new GameObject("Goose_" + index);
            go.transform.SetParent(mPlayerRoot, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = CurrentGooseSprite();
            GooseView view = new GooseView { Root = go, Renderer = renderer, ShootTimer = index == 0 || mGooseViews.Count == 0 ? UnityEngine.Random.Range(0.03f, 0.28f) : mGooseViews[0].ShootTimer };
            ResetGooseHealth(view);
            CreateGooseHealthBar(view);
            return view;
        }

        private void ResetGooseHealth(GooseView view)
        {
            if (view == null)
            {
                return;
            }

            view.MaxHealth = GetGooseMaxHealth();
            view.Health = view.MaxHealth;
            UpdateGooseHealthBar(view);
        }

        private void EnsureGooseHealth(GooseView view)
        {
            if (view == null)
            {
                return;
            }

            int maxHealth = GetGooseMaxHealth();
            if (view.MaxHealth <= 0)
            {
                view.MaxHealth = maxHealth;
                view.Health = maxHealth;
                return;
            }

            if (view.MaxHealth != maxHealth)
            {
                view.Health = Mathf.Clamp(view.Health, 1, maxHealth);
                view.MaxHealth = maxHealth;
            }
            UpdateGooseHealthBar(view);
        }

        private void CreateGooseHealthBar(GooseView view)
        {
            if (view == null || view.Root == null || view.HealthBack != null)
            {
                return;
            }

            GameObject back = CreateSpriteObject("GooseHealthBack", view.Root.transform, mSquareSprite, new Color(0.08f, 0.08f, 0.08f, 0.68f), new Vector3(0f, 0.72f, 0f), new Vector3(0.56f, 0.07f, 1f), 38);
            GameObject fill = CreateSpriteObject("GooseHealthFill", view.Root.transform, mSquareSprite, new Color(0.18f, 0.84f, 0.34f), new Vector3(0f, 0.72f, 0f), new Vector3(0.56f, 0.07f, 1f), 39);
            view.HealthBack = back.GetComponent<SpriteRenderer>();
            view.HealthFill = fill.GetComponent<SpriteRenderer>();
            UpdateGooseHealthBar(view);
        }

        private void UpdateGooseHealthBar(GooseView view)
        {
            if (view == null || view.HealthBack == null || view.HealthFill == null)
            {
                return;
            }

            float percent = view.MaxHealth <= 0 ? 0f : Mathf.Clamp01(view.Health / (float)view.MaxHealth);
            view.HealthFill.transform.localScale = new Vector3(0.56f * percent, 0.07f, 1f);
            view.HealthFill.color = Color.Lerp(new Color(0.93f, 0.12f, 0.12f), new Color(0.18f, 0.84f, 0.34f), percent);
            bool visible = percent > 0f && percent < 0.999f;
            view.HealthBack.gameObject.SetActive(visible);
            view.HealthFill.gameObject.SetActive(visible);
        }

        private void RefreshGooseWeaponArt()
        {
            for (int i = 0; i < mGooseViews.Count; i++)
            {
                mGooseViews[i].Renderer.sprite = CurrentGooseSprite();
                mGooseViews[i].Renderer.color = CurrentGooseTint();
            }
        }

        private void SpawnGate(int lane, int value, float logicalY = 0f, int gateGroup = 0)
        {
            ActorConfig actor = Actor(10);
            Entity gate = CreateEntity(EntityKind.Gate, "Gate", lane, logicalY > 0f ? LogicalToWorldY(logicalY) : SpawnY, ActorMoveSpeed(10, BaseFallSpeed));
            ApplyActorToEntity(gate, actor);
            gate.Amount = value;
            gate.GateGroup = gateGroup;
            gate.Body.sprite = mMultiplierGateSprite != null ? mMultiplierGateSprite : (mNormalGateSprite != null ? mNormalGateSprite : mSquareSprite);
            gate.Body.color = Color.white;
            SetEntityBaseScale(gate, new Vector3(1.64f, 1.64f, 1f));
            gate.Label = CreateOutlinedWorldLabel(gate.Root.transform, "", new Vector3(0f, 0.03f, 0f), 0.153f, Color.white, Color.black, out gate.LabelOutlines);
            UpdateGateVisual(gate, false);
            mEntities.Add(gate);
        }

        private void SpawnGooseCage(int lane, float logicalY = 0f, int health = 0, int rewardCount = 0)
        {
            MonsterConfigData monster = GooseCageMonster();
            int actorId = monster != null && monster.actorId > 0 ? monster.actorId : 9;
            ActorConfig actor = Actor(actorId);
            Entity cage = CreateEntity(EntityKind.GooseCage, "GooseCage", lane, logicalY > 0f ? LogicalToWorldY(logicalY) : SpawnY, ActorMoveSpeed(actorId, BaseFallSpeed));
            ApplyActorToEntity(cage, actor);
            cage.Health = Mathf.Max(1, health > 0 ? health : GooseCageBloodValue(monster, 1, Mathf.RoundToInt(ActorStat(actorId, "hpMax", 3f))));
            cage.MaxHealth = cage.Health;
            cage.Amount = Mathf.Max(1, rewardCount > 0 ? rewardCount : GooseCageBloodValue(monster, 2, monster != null && monster.count > 0 ? monster.count : 3));
            cage.Body.sprite = mGooseCageSprite != null ? mGooseCageSprite : (mNormalGateSprite != null ? mNormalGateSprite : mSquareSprite);
            cage.Body.color = Color.white;
            SetEntityBaseScale(cage, new Vector3(2.10f, 1.32f, 1f));
            cage.Label = CreateOutlinedWorldLabel(cage.Root.transform, cage.Health.ToString(), new Vector3(0f, 0.03f, 0f), 0.14f, Color.white, Color.black, out cage.LabelOutlines);
            cage.RewardLabel = CreateWorldLabel(cage.Root.transform, "+" + cage.Amount, new Vector3(0f, 0.92f, 0f), 0.14f, Color.black);
            Vector3 labelScale = new Vector3(1.64f / 2.10f, 1.64f / 1.32f, 1f);
            cage.Label.transform.parent.localScale = labelScale;
            cage.RewardLabel.transform.localScale = labelScale;
            mEntities.Add(cage);
        }

        private void SpawnElementGate(int lane, ElementKind element, float logicalY = 0f, int health = 0)
        {
            int actorId = element == ElementKind.Lightning ? 12 : 11;
            ActorConfig actor = Actor(actorId);
            Entity gate = CreateEntity(EntityKind.ElementGate, ElementName(element) + "Gate", lane, logicalY > 0f ? LogicalToWorldY(logicalY) : SpawnY, ActorMoveSpeed(actorId, BaseFallSpeed));
            ApplyActorToEntity(gate, actor);
            gate.Element = element;
            gate.Health = Mathf.Max(1, health > 0 ? health : Mathf.RoundToInt(Mathf.Max(20f, mGooseCount * GetWeaponDamage() * 0.75f)));
            gate.MaxHealth = gate.Health;
            gate.Body.sprite = ElementGateSprite(element);
            gate.Body.color = Color.white;
            SetEntityBaseScale(gate, new Vector3(1.64f, 1.64f, 1f));
            gate.Label = CreateOutlinedWorldLabel(gate.Root.transform, gate.Health.ToString(), new Vector3(0f, 0.03f, 0f), 0.153f, Color.white, Color.black, out gate.LabelOutlines);
            mEntities.Add(gate);
        }

        private void SpawnWeaponRack(int lane, WeaponKind weapon, float logicalY = 0f, int health = 0)
        {
            int actorId = weapon == WeaponKind.Staff ? 8 : 7;
            ActorConfig actor = Actor(actorId);
            Entity rack = CreateEntity(EntityKind.WeaponRack, WeaponName(weapon) + "Rack", lane, logicalY > 0f ? LogicalToWorldY(logicalY) : SpawnY, ActorMoveSpeed(actorId, BaseFallSpeed));
            ApplyActorToEntity(rack, actor);
            rack.Weapon = weapon;
            rack.Health = Mathf.Max(1, health > 0 ? health : Mathf.RoundToInt(Mathf.Max(1f, ActorStat(actorId, "hpMax", 1f))));
            rack.MaxHealth = rack.Health;
            rack.Body.sprite = weapon == WeaponKind.Bow ? mBowRackSprite : mStaffRackSprite;
            rack.Body.color = Color.white;
            SetEntityBaseScale(rack, new Vector3(1.31f, 1.31f, 1f));
            rack.Label = CreateOutlinedWorldLabel(rack.Root.transform, rack.Health.ToString(), new Vector3(0f, 0.03f, 0f), 0.14f, Color.white, Color.black, out rack.LabelOutlines);
            mEntities.Add(rack);
        }

        private void SpawnChicken(int lane, ChickenKind kind, float y, float sideOffset)
        {
            int actorId = ChickenActorId(kind);
            ActorConfig actor = Actor(actorId);
            Entity chicken = CreateEntity(EntityKind.Chicken, kind + "Chicken", lane, y, ChickenSpeed(kind));
            ApplyActorToEntity(chicken, actor);
            chicken.LogicalOffsetX = sideOffset / Mathf.Max(0.001f, PerspectiveXScale(chicken.LogicalY));
            SyncEntityPosition(chicken);
            chicken.Chicken = kind;
            chicken.Health = Mathf.RoundToInt(ActorStat(actorId, "hpMax", ChickenHealth(kind)));
            chicken.MaxHealth = chicken.Health;
            chicken.MeleeTimer = EnemyMeleeCooldown;
            chicken.Body.color = Color.white;
            chicken.Transform.localRotation = Quaternion.identity;
            chicken.BaseScale = ChickenScale(kind);
            SetupChickenWalkVideo(chicken);
            UpdateChickenVisual(chicken);
            CreateEnemyHealthBar(chicken, new Color(0.9f, 0.18f, 0.16f));
            mEntities.Add(chicken);
        }

        private Entity CreateEntity(EntityKind kind, string name, int lane, float y, float speed)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(mEntityRoot, false);
            int lineX = Mathf.Clamp(lane, -1, 1);
            float logicalY = WorldToLogicalY(y);
            root.transform.position = new Vector3(LogicalToWorldX(lineX, logicalY), LogicalToWorldY(logicalY), 0f);
            SpriteRenderer body = root.AddComponent<SpriteRenderer>();
            body.sprite = mSquareSprite;
            body.color = Color.white;
            body.sortingOrder = kind == EntityKind.Bullet ? 30 : 12;
            return new Entity { Kind = kind, Root = root, Transform = root.transform, Body = body, Speed = speed, BreakRadius = 0.25f, LogicalX = lineX, LogicalY = logicalY, Amount = 1, Health = 1, MaxHealth = 1, Damage = 1, Defense = 1f, BaseScale = Vector3.one };
        }

        private void ApplyActorToEntity(Entity entity, ActorConfig actor)
        {
            if (actor == null)
            {
                return;
            }
            entity.ActorId = actor.Id;
            entity.BreakRadius = actor.BreakRadius;
            entity.Speed = ActorMoveSpeed(actor.Id, entity.Speed);
            entity.Damage = Mathf.Max(1, Mathf.RoundToInt(ActorStat(actor.Id, "atk", entity.Damage)));
            entity.Defense = Mathf.Max(1f, ActorStat(actor.Id, "def", 1f));
        }

        private void UpdateGateVisual(Entity gate, bool pop)
        {
            SetWorldLabelText(gate, gate.Amount >= 0 ? "+" + gate.Amount : gate.Amount.ToString());
            gate.Label.transform.localScale = Vector3.one * (pop ? 1.2f : 1f);
            if (gate.LabelOutlines != null)
            {
                for (int i = 0; i < gate.LabelOutlines.Length; i++)
                {
                    if (gate.LabelOutlines[i] != null)
                    {
                        gate.LabelOutlines[i].transform.localScale = gate.Label.transform.localScale;
                    }
                }
            }
        }

        private void UpdateElementGateVisual(Entity gate)
        {
            gate.Body.color = Color.Lerp(Color.white, GetElementColor(gate.Element), 0.25f);
            SetWorldLabelText(gate, "");
            SpawnText(gate.Transform.position, ElementGateBreakText(gate.Element), GetElementColor(gate.Element), 0.42f);
        }

        private Vector3 GetGooseDeathEffectPosition(GooseView view)
        {
            if (view == null || view.Root == null)
            {
                return mPlayerRoot != null ? mPlayerRoot.position : new Vector3(0f, PlayerY, 0f);
            }

            return view.Renderer != null ? view.Renderer.bounds.center : view.Root.transform.position;
        }

        private void SpawnGooseDeathEffect(Vector3 position)
        {
            VideoClip clip = GooseDeathClipForWeapon();
            if (clip == null)
            {
                SpawnText(position, "倒", new Color(0.95f, 0.78f, 0.24f), 0.36f);
                return;
            }

            SpawnGooseVideoEffect("GooseDeath_" + mWeapon, clip, position + new Vector3(0f, 0.08f, -0.05f), 1.18f, 46);
        }

        private void SpawnGooseVideoEffect(string name, VideoClip clip, Vector3 position, float scale, int sortingOrder)
        {
            SpawnGooseVideoEffect(name, clip, mEntityRoot, position, Vector3.one * scale, sortingOrder, false);
        }

        private void SpawnGooseVideoEffect(string name, VideoClip clip, Transform parent, Vector3 localPosition, float worldScale, int sortingOrder)
        {
            SpawnGooseVideoEffect(name, clip, parent, localPosition, Vector3.one * worldScale, sortingOrder, true);
        }

        private GameObject SpawnGooseVideoEffect(string name, VideoClip clip, Transform parent, Vector3 position, Vector3 worldScale, int sortingOrder, bool useLocalPosition)
        {
            return SpawnGooseVideoEffect(name, clip, parent, position, worldScale, sortingOrder, useLocalPosition, 1f, -1f, 0f);
        }

        private GameObject SpawnGooseVideoEffect(string name, VideoClip clip, Transform parent, Vector3 position, Vector3 worldScale, int sortingOrder, bool useLocalPosition, float playbackSpeed, float lifeOverride, float startTime)
        {
            if (clip == null)
            {
                return null;
            }

            RenderTexture texture = new RenderTexture(384, 384, 0, RenderTextureFormat.ARGB32);
            texture.Create();

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent != null ? parent : mEntityRoot, false);
            if (useLocalPosition)
            {
                quad.transform.localPosition = position;
                float parentScale = Mathf.Max(0.001f, quad.transform.parent.lossyScale.x);
                quad.transform.localScale = new Vector3(worldScale.x / parentScale, worldScale.y / parentScale, worldScale.z);
            }
            else
            {
                quad.transform.position = position;
                quad.transform.localScale = worldScale;
            }
            Collider collider = quad.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = quad.GetComponent<Renderer>();
            Material material = new Material(mGooseDeathChromaKeyShader != null ? mGooseDeathChromaKeyShader : Shader.Find("Unlit/Texture"));
            material.mainTexture = texture;
            if (mGooseDeathChromaKeyShader != null)
            {
                material.SetColor("_KeyColor", Color.green);
                material.SetFloat("_Threshold", 0.035f);
                material.SetFloat("_Feather", 0.16f);
            }
            renderer.material = material;
            renderer.sortingOrder = sortingOrder;

            VideoPlayer player = quad.AddComponent<VideoPlayer>();
            player.clip = clip;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = texture;
            player.isLooping = false;
            player.playOnAwake = false;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.sendFrameReadyEvents = true;
            player.playbackSpeed = Mathf.Max(0.01f, playbackSpeed);
            if (startTime > 0.001f)
            {
                player.time = Mathf.Min(startTime, Mathf.Max(0f, (float)clip.length - 0.01f));
            }
            player.Play();

            float life = lifeOverride > 0f
                ? lifeOverride
                : (clip.length > 0.01 ? (float)clip.length + 0.05f : 0.8f);
            mEntities.Add(new Entity
            {
                Kind = EntityKind.Effect,
                Root = quad,
                Transform = quad.transform,
                Life = life,
                EffectTexture = texture,
                EffectMaterial = material,
                EffectVideoPlayer = player
            });
            return quad;
        }

        private VideoClip GooseDeathClipForWeapon()
        {
            if (mWeapon == WeaponKind.Bow)
            {
                return mGooseBowDeathClip ?? mGooseSlingshotDeathClip;
            }
            if (mWeapon == WeaponKind.Staff)
            {
                return mGooseStaffDeathClip ?? mGooseSlingshotDeathClip;
            }
            return mGooseSlingshotDeathClip;
        }

        private VideoClip GooseAttackClipForWeapon()
        {
            if (mWeapon == WeaponKind.Bow)
            {
                return mGooseBowAttackClip ?? mGooseSlingshotAttackClip;
            }
            if (mWeapon == WeaponKind.Staff)
            {
                return mGooseStaffAttackClip ?? mGooseSlingshotAttackClip;
            }
            return mGooseSlingshotAttackClip;
        }

        private void SpawnDeathEffect(Entity chicken, AttackKind attack)
        {
            string label = "鸽鸽";
            Color color = new Color(1f, 0.86f, 0.08f);
            if (attack == AttackKind.Fire) { label = "rap"; color = new Color(0.62f, 0.22f, 0.9f); }
            else if (attack == AttackKind.Lightning) { label = "篮球"; color = new Color(0.55f, 0.32f, 0.12f); }
            else if (attack == AttackKind.Ice) { label = "冻鸡"; color = new Color(0.55f, 0.9f, 1f); }
            else if (attack == AttackKind.Overload) { label = "鸡块"; color = new Color(1f, 0.35f, 0.05f); }
            else if (attack == AttackKind.Vaporize) { label = "热汽"; color = new Color(0.9f, 0.95f, 1f); }
            else if (attack == AttackKind.FrostThunder) { label = "冰裂"; color = new Color(0.58f, 0.88f, 1f); }
            else if (attack == AttackKind.Laser) { label = "练习生"; color = new Color(0.68f, 0.95f, 1f); }
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
            float radius = GetGooseFormationRadius(mGooseCount) + 0.62f;
            return new Rect(mPlayerRoot.position.x - radius, PlayerY - radius, radius * 2f, radius * 2f);
        }

        private float GetGooseFormationRadius(int count)
        {
            if (count <= 1)
            {
                return 0f;
            }

            int ring = 1;
            int ringStart = 1;
            int lastIndex = Mathf.Max(0, count - 1);
            while (lastIndex >= ringStart + ring * 6)
            {
                ringStart += ring * 6;
                ring++;
            }
            return ring * GooseRingSpacing;
        }

        private bool PlayerOverlapsEntityRadius(Entity entity, Rect playerRect)
        {
            Vector2 center = new Vector2(entity.Transform.position.x, entity.Transform.position.y);
            Vector2 closest = new Vector2(Mathf.Clamp(center.x, playerRect.xMin, playerRect.xMax), Mathf.Clamp(center.y, playerRect.yMin, playerRect.yMax));
            float radius = Mathf.Max(0.55f, entity.BreakRadius);
            return (closest - center).sqrMagnitude <= radius * radius;
        }

        private Rect GetEntityRect(Entity entity)
        {
            Vector3 p = entity.Transform.position;
            if (entity.Kind == EntityKind.Gate || entity.Kind == EntityKind.ElementGate)
            {
                return new Rect(p.x - 0.55f, p.y - 0.8f, 1.1f, 1.6f);
            }
            if (entity.Kind == EntityKind.GooseCage)
            {
                float size = Mathf.Max(0.75f, entity.BreakRadius * 2f);
                return new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size);
            }
            if (entity.Kind == EntityKind.WeaponRack)
            {
                return new Rect(p.x - 0.48f, p.y - 0.55f, 0.96f, 1.1f);
            }
            if (entity.Kind == EntityKind.Chicken)
            {
                float size = Mathf.Max(entity.BreakRadius * 2f, entity.Chicken == ChickenKind.Boss ? 1.1f : entity.Chicken == ChickenKind.Fat ? 0.88f : 0.72f);
                return new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size);
            }
            if (entity.Kind == EntityKind.Bullet)
            {
                float size = Mathf.Max(0.12f, entity.BreakRadius * 2f);
                return new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size);
            }
            return new Rect(p.x - 0.25f, p.y - 0.25f, 0.5f, 0.5f);
        }

        private void HandleInput()
        {
            float pointerWorldDeltaX;
            if (TryReadPointerWorldDeltaX(out pointerWorldDeltaX))
            {
                mTargetX = Mathf.Clamp(mTargetX + pointerWorldDeltaX, GetLeftBound(), GetRightBound());
            }

            float axis = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(axis) > 0.1f)
            {
                mTargetX = Mathf.Clamp(mTargetX + axis * PlayerMoveSpeed * Time.deltaTime, GetLeftBound(), GetRightBound());
            }
        }

        private bool TryReadPointerWorldDeltaX(out float worldDeltaX)
        {
            float screenX;
            float screenDeltaX;
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    ResetPointerDrag();
                    worldDeltaX = 0f;
                    return false;
                }

                screenX = touch.position.x;
                if (touch.phase == TouchPhase.Began || !mPointerDragging)
                {
                    mPointerDragging = true;
                    mLastPointerScreenX = screenX;
                    worldDeltaX = 0f;
                    return false;
                }

                screenDeltaX = touch.deltaPosition.x;
                mLastPointerScreenX = screenX;
            }
            else if (Input.GetMouseButton(0))
            {
                screenX = Input.mousePosition.x;
                if (Input.GetMouseButtonDown(0) || !mPointerDragging)
                {
                    mPointerDragging = true;
                    mLastPointerScreenX = screenX;
                    worldDeltaX = 0f;
                    return false;
                }

                screenDeltaX = screenX - mLastPointerScreenX;
                mLastPointerScreenX = screenX;
            }
            else
            {
                ResetPointerDrag();
                worldDeltaX = 0f;
                return false;
            }

            if (Mathf.Abs(screenDeltaX) < 0.01f)
            {
                worldDeltaX = 0f;
                return false;
            }

            float cameraWidth = mCamera.orthographicSize * 2f * mCamera.aspect;
            float worldUnitsPerPixel = cameraWidth / Mathf.Max(1, Screen.width);
            worldDeltaX = screenDeltaX * worldUnitsPerPixel * PointerDragSensitivity;
            return true;
        }

        private void ResetPointerDrag()
        {
            mPointerDragging = false;
            mLastPointerScreenX = 0f;
        }

        private int NearestLane(float x)
        {
            return Mathf.Clamp(Mathf.RoundToInt(x / Mathf.Max(0.001f, NearLaneOffset)), -1, 1);
        }

        private void RefreshHud()
        {
            float progress = mTargetChickenCount > 0
                ? Mathf.Clamp01(mKilledChickenCount / (float)mTargetChickenCount)
                : 0f;
            mKillProgressFill.anchorMax = new Vector2(progress, 1f);


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

        private TMP_Text CreateHudText(string name, Transform parent, Vector2 anchoredPosition, TextAnchor anchor, int size, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = new Vector2(660f, 80f);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = mHudFont;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(12, size - 6);
            text.fontSizeMax = size;
            text.richText = false;
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
            TMP_Text text = CreateHudText("LabelText", go.transform, Vector2.zero, TextAnchor.MiddleCenter, 28, Color.white);
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

        private TextMesh CreateOutlinedWorldLabel(Transform parent, string text, Vector3 localPosition, float size, Color fillColor, Color outlineColor, out TextMesh[] outlines)
        {
            GameObject root = new GameObject("OutlinedLabel");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            Vector3[] offsets =
            {
                new Vector3(-0.012f, 0f, 0f),
                new Vector3(0.012f, 0f, 0f),
                new Vector3(0f, -0.012f, 0f),
                new Vector3(0f, 0.012f, 0f),
                new Vector3(-0.009f, -0.009f, 0f),
                new Vector3(-0.009f, 0.009f, 0f),
                new Vector3(0.009f, -0.009f, 0f),
                new Vector3(0.009f, 0.009f, 0f),
            };

            outlines = new TextMesh[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                outlines[i] = CreateWorldLabel(root.transform, text, offsets[i], size, outlineColor);
                outlines[i].name = "LabelOutline";
                outlines[i].GetComponent<MeshRenderer>().sortingOrder = 90;
            }

            TextMesh front = CreateWorldLabel(root.transform, text, Vector3.zero, size, fillColor);
            front.name = "LabelFill";
            front.GetComponent<MeshRenderer>().sortingOrder = 91;
            return front;
        }

        private void SetWorldLabelText(Entity entity, string text)
        {
            if (entity == null)
            {
                return;
            }
            if (entity.Label != null)
            {
                entity.Label.text = text;
            }
            if (entity.LabelOutlines == null)
            {
                return;
            }
            for (int i = 0; i < entity.LabelOutlines.Length; i++)
            {
                if (entity.LabelOutlines[i] != null)
                {
                    entity.LabelOutlines[i].text = text;
                }
            }
        }

        private float GetWeaponCooldown()
        {
            float atkSpeed = Mathf.Max(0.1f, ActorStat(GooseActorIdForWeapon(), "atkspd", 1f));
            return 1f / atkSpeed;
        }

        private int GetWeaponDamage()
        {
            return Mathf.Max(1, Mathf.RoundToInt(ActorStat(GooseActorIdForWeapon(), "atk", 1f)));
        }

        private float FireBurnRate()
        {
            return mFireLevel >= 3 ? 0.07f : mFireLevel == 2 ? 0.04f : 0.02f;
        }

        private AttackKind ChooseAttack()
        {
            if (mFireLevel > 0 && mLightningLevel > 0) return AttackKind.Laser;
            if (mFireLevel > 0 && mIceLevel > 0) return AttackKind.Vaporize;
            if (mLightningLevel > 0 && mIceLevel > 0) return AttackKind.FrostThunder;
            if (mFireLevel > 0) return AttackKind.Fire;
            if (mLightningLevel > 0) return AttackKind.Lightning;
            if (mIceLevel > 0) return AttackKind.Ice;
            return AttackKind.Normal;
        }

        private Sprite CurrentGooseSprite()
        {
            if (mWeapon == WeaponKind.Bow)
            {
                return mGooseBowSprite ?? mGooseSlingshotSprite;
            }
            if (mWeapon == WeaponKind.Staff)
            {
                return mGooseStaffSprite ?? mGooseSlingshotSprite;
            }
            return mGooseSlingshotSprite;
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

        private void SetupChickenWalkVideo(Entity chicken)
        {
            if (chicken == null || chicken.Root == null || mChickenWalkClip == null || mGooseDeathChromaKeyShader == null)
            {
                return;
            }

            RenderTexture texture = new RenderTexture(360, 640, 0, RenderTextureFormat.ARGB32);
            texture.Create();

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "ChickenWalkVideo";
            quad.transform.SetParent(chicken.Root.transform, false);
            quad.transform.localPosition = Vector3.zero;
            Collider collider = quad.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = quad.GetComponent<Renderer>();
            Material material = new Material(mGooseDeathChromaKeyShader);
            material.mainTexture = texture;
            material.SetColor("_KeyColor", Color.green);
            material.SetFloat("_Threshold", 0.035f);
            material.SetFloat("_Feather", 0.16f);
            renderer.material = material;

            VideoPlayer player = quad.AddComponent<VideoPlayer>();
            player.clip = mChickenWalkClip;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = texture;
            player.isLooping = true;
            player.playOnAwake = false;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.Play();

            chicken.EffectTexture = texture;
            chicken.EffectMaterial = material;
            chicken.EffectVideoPlayer = player;
        }

        private void UpdateChickenVisual(Entity chicken)
        {
            Vector3 scale = GetEntityPerspectiveScale(chicken);
            int sortingOrder = EntityPerspectiveSortingOrder(chicken.LogicalY);
            chicken.Body.sprite = mChickenSprite ?? mChickenLeftSprite;
            chicken.Body.sortingOrder = sortingOrder;

            if (chicken.EffectVideoPlayer != null)
            {
                chicken.Body.color = new Color(1f, 1f, 1f, 0f);
                Transform video = chicken.EffectVideoPlayer.transform;
                video.localPosition = Vector3.zero;
                video.localScale = new Vector3(scale.y * 1.755f, scale.y * 3.12f, 1f);
                Renderer renderer = video.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sortingOrder = sortingOrder;
                }
                return;
            }

            chicken.Body.color = Color.white;
            chicken.Body.transform.localScale = scale;
        }

        private void SetEntityBaseScale(Entity entity, Vector3 baseScale)
        {
            entity.BaseScale = baseScale;
            ApplyEntityPerspective(entity);
        }

        private void ApplyEntityPerspective(Entity entity)
        {
            if (entity == null || entity.Body == null || entity.Kind == EntityKind.Effect)
            {
                return;
            }

            entity.Body.transform.localScale = GetEntityPerspectiveScale(entity);
            entity.Body.sortingOrder = EntityPerspectiveSortingOrder(entity.LogicalY);
        }

        private Vector3 GetEntityPerspectiveScale(Entity entity)
        {
            Vector3 baseScale = entity.BaseScale == Vector3.zero ? Vector3.one : entity.BaseScale;
            return baseScale * EntityPerspectiveScale(entity.LogicalY);
        }

        private float EntityPerspectiveScale(float logicalY)
        {
            float progress = LogicalPerspectiveProgress(logicalY);
            return Mathf.Lerp(ApproachNearScale, ApproachFarScale, progress);
        }

        private float PerspectiveMotionScale(float logicalY)
        {
            return Mathf.Lerp(ApproachNearScale, ApproachFarScale, LogicalPerspectiveProgress(logicalY));
        }

        private int EntityPerspectiveSortingOrder(float logicalY)
        {
            return Mathf.RoundToInt(Mathf.Lerp(19f, 10f, LogicalPerspectiveProgress(logicalY)));
        }

        private float LogicalPerspectiveProgress(float logicalY)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(logicalY / LogicalMaxY));
        }

        private void SyncEntityPosition(Entity entity)
        {
            float logicalX = entity.LogicalX + entity.LogicalOffsetX;
            entity.Transform.position = new Vector3(LogicalToWorldX(logicalX, entity.LogicalY), LogicalToWorldY(entity.LogicalY), entity.Transform.position.z);
        }

        private float LogicalToWorldX(float logicalX, float logicalY)
        {
            return logicalX * PerspectiveXScale(logicalY);
        }

        private float WorldToLogicalX(float worldX, float logicalY)
        {
            return worldX / Mathf.Max(0.001f, PerspectiveXScale(logicalY));
        }

        private float WorldDistanceToLogicalX(float worldDistance, float logicalY)
        {
            return worldDistance / Mathf.Max(0.001f, PerspectiveXScale(logicalY));
        }

        private float GetPlayerMeleeLogicalX()
        {
            return WorldToLogicalX(mPlayerRoot.position.x, WorldToLogicalY(EnemyMeleeY));
        }

        private void UpdateLogicalPosition(Entity entity)
        {
            entity.LogicalY = WorldToLogicalY(entity.Transform.position.y);
            entity.LogicalX = WorldToLogicalX(entity.Transform.position.x, entity.LogicalY) - entity.LogicalOffsetX;
        }

        private float LogicalOriginWorldY()
        {
            // Keep logical (0, 0) aligned with Unity world (0, 0) for direct comparison.
            return 0f;
        }

        private bool IsEntityBelowDespawn(Entity entity)
        {
            if (entity.Kind == EntityKind.Effect)
            {
                return entity.Transform.position.y < DespawnY;
            }

            return entity.LogicalY < WorldToLogicalY(DespawnY);
        }

        private float LogicalToWorldY(float logicalY)
        {
            return Mathf.LerpUnclamped(LogicalOriginWorldY(), SpawnY, logicalY / LogicalMaxY);
        }

        private float WorldToLogicalY(float worldY)
        {
            float distance = SpawnY - LogicalOriginWorldY();
            return Mathf.Approximately(distance, 0f) ? 0f : (worldY - LogicalOriginWorldY()) / distance * LogicalMaxY;
        }

        private float WorldDistanceToLogicalY(float worldDistance)
        {
            return worldDistance / Mathf.Max(0.001f, SpawnY - LogicalOriginWorldY()) * LogicalMaxY;
        }

        private float PerspectiveXScale(float logicalY)
        {
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(logicalY / LogicalMaxY));
            return Mathf.Lerp(LogicalToWorldNearScale, LogicalToWorldFarScale, progress);
        }

        private int ChickenHealth(ChickenKind kind)
        {
            return Mathf.Max(1, Mathf.RoundToInt(ActorStat(ChickenActorId(kind), "hpMax", 1f)));
        }

        private float ChickenSpeed(ChickenKind kind)
        {
            return ActorMoveSpeed(ChickenActorId(kind), BaseFallSpeed);
        }

        private int GooseActorIdForWeapon()
        {
            if (mActiveWeaponBuffId == 2) return 2;
            if (mActiveWeaponBuffId == 3) return 3;
            return 1;
        }

        private int ChickenActorId(ChickenKind kind)
        {
            if (kind == ChickenKind.Fat) return 5;
            if (kind == ChickenKind.Fast || kind == ChickenKind.Boss) return 6;
            return 4;
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

        private string ElementGateBreakText(ElementKind element)
        {
            if (element == ElementKind.Fire) return "鸡你太美";
            if (element == ElementKind.Lightning) return "铁山靠";
            return "锁碎";
        }

        private string ElementAcquireText(ElementKind element)
        {
            if (element == ElementKind.Fire) return "鸡神之力";
            if (element == ElementKind.Lightning) return "坤神之力";
            return ElementName(element) + "+1";
        }

        private string WeaponName(WeaponKind weapon)
        {
            return weapon == WeaponKind.Bow ? "弓" : weapon == WeaponKind.Staff ? "法杖" : "弹弓";
        }

        private float GetLeftBound()
        {
            return LogicalToWorldX(PlayerMinLogicalX, WorldToLogicalY(PlayerY));
        }

        private float GetRightBound()
        {
            return LogicalToWorldX(PlayerMaxLogicalX, WorldToLogicalY(PlayerY));
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


















