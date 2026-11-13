using CarcavelosCircuit.Shared;
using CarcavelosCircuit.Visuals;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CarcavelosCircuit.Tests.EditMode
{
    // V28 (Role 3): the time-of-day choice (Shared) and the campus world switching its look (Visuals).
    public class TimeOfDayTests
    {
        private bool hadSaved;
        private int saved;
        private GameObject world;
        private readonly System.Collections.Generic.List<Object> made = new System.Collections.Generic.List<Object>();

        [SetUp]
        public void KeepPlayerChoice()
        {
            hadSaved = PlayerPrefs.HasKey(TimeOfDayChoice.PrefsKey);
            saved = PlayerPrefs.GetInt(TimeOfDayChoice.PrefsKey, 0);
        }

        [TearDown]
        public void RestorePlayerChoice()
        {
            TimeOfDayChoice.Set(TimeOfDayChoice.FromSaved(saved));
            if (!hadSaved)
            {
                PlayerPrefs.DeleteKey(TimeOfDayChoice.PrefsKey);
            }
            if (world != null)
            {
                Object.DestroyImmediate(world);
            }
            foreach (Object o in made)
            {
                Object.DestroyImmediate(o);
            }
            made.Clear();
            Shader.SetGlobalFloat(CampusWorldLighting.BakedShadowDropProperty, 0f);
        }

        [Test]
        public void Next_CyclesNoonSunsetNoon_BothWays()
        {
            Assert.AreEqual(TimeOfDay.Sunset, TimeOfDayChoice.Next(TimeOfDay.Noon));
            Assert.AreEqual(TimeOfDay.Noon, TimeOfDayChoice.Next(TimeOfDay.Sunset));
            Assert.AreEqual(TimeOfDay.Sunset, TimeOfDayChoice.Next(TimeOfDay.Noon, -1));
            Assert.AreEqual(TimeOfDay.Noon, TimeOfDayChoice.Next(TimeOfDay.Sunset, -1));
        }

        [Test]
        public void FromSaved_UnknownNumbersBecomeNoon()
        {
            Assert.AreEqual(TimeOfDay.Noon, TimeOfDayChoice.FromSaved(0));
            Assert.AreEqual(TimeOfDay.Sunset, TimeOfDayChoice.FromSaved(1));
            Assert.AreEqual(TimeOfDay.Noon, TimeOfDayChoice.FromSaved(2), "a later Night in an older build");
            Assert.AreEqual(TimeOfDay.Noon, TimeOfDayChoice.FromSaved(-1));
        }

        [Test]
        public void Label_IsWhatTheMenuShows()
        {
            Assert.AreEqual("NOON", TimeOfDayChoice.Label(TimeOfDay.Noon));
            Assert.AreEqual("SUNSET", TimeOfDayChoice.Label(TimeOfDay.Sunset));
        }

        [Test]
        public void Set_SavesAndTellsListenersOnlyOnARealChange()
        {
            TimeOfDayChoice.Set(TimeOfDay.Noon);
            int calls = 0;
            TimeOfDay heard = TimeOfDay.Noon;
            System.Action<TimeOfDay> listener = m => { calls++; heard = m; };
            TimeOfDayChoice.Changed += listener;
            try
            {
                Assert.IsTrue(TimeOfDayChoice.Set(TimeOfDay.Sunset));
                Assert.IsFalse(TimeOfDayChoice.Set(TimeOfDay.Sunset), "same mood again changes nothing");
                Assert.AreEqual(1, calls);
                Assert.AreEqual(TimeOfDay.Sunset, heard);
                Assert.AreEqual(TimeOfDay.Sunset, TimeOfDayChoice.Current);
                Assert.AreEqual((int)TimeOfDay.Sunset, PlayerPrefs.GetInt(TimeOfDayChoice.PrefsKey, -1));
            }
            finally
            {
                TimeOfDayChoice.Changed -= listener;
            }
        }

        [Test]
        public void TowardsSun_PointsWhereTheSkyPaintsTheSun()
        {
            // azimuth clockwise from north (+z): 90 = east (+x), 180 = south (-z); elevation lifts it
            Vector3 east = TimeOfDayLook.TowardsSun(90f, 0f);
            Assert.AreEqual(1f, east.x, 1e-4f);
            Assert.AreEqual(0f, east.y, 1e-4f);
            Vector3 south = TimeOfDayLook.TowardsSun(180f, 0f);
            Assert.AreEqual(-1f, south.z, 1e-4f);
            Vector3 high = TimeOfDayLook.TowardsSun(250f, 30f);
            Assert.AreEqual(0.5f, high.y, 1e-4f);
            Assert.AreEqual(1f, high.magnitude, 1e-4f);
        }

        [Test]
        public void SunRotation_ShinesFromTheSunTowardsTheGround()
        {
            TimeOfDayLook look = Make<TimeOfDayLook>();
            look.sunAzimuth = 250f;
            look.sunElevation = 6f;
            Vector3 shine = look.SunRotation * Vector3.forward;
            Assert.Less(shine.y, 0f, "light goes down");
            Assert.Greater(Vector3.Dot(-shine, TimeOfDayLook.TowardsSun(250f, 6f)), 0.9999f);
        }

        [Test]
        public void Lighting_SwitchesSunVolumeAndBakedShadowWithTheMood()
        {
            TimeOfDayLook noon = Look(TimeOfDay.Noon, 225f, 42f, 1f);
            TimeOfDayLook sunset = Look(TimeOfDay.Sunset, 250f, 6f, 0f);
            CampusWorldLighting lighting = World(noon, sunset, out Light sun, out Volume volume);

            lighting.ShowInEditor(TimeOfDay.Sunset);
            Assert.Less(Quaternion.Angle(sun.transform.rotation, sunset.SunRotation), 0.01f);
            Assert.AreEqual(sunset.sunIntensity, sun.intensity);
            Assert.AreSame(sunset.profile, volume.sharedProfile);
            Assert.AreEqual(1f, Shader.GetGlobalFloat(CampusWorldLighting.BakedShadowDropProperty), 1e-5f);

            lighting.ShowInEditor(TimeOfDay.Noon);
            Assert.Less(Quaternion.Angle(sun.transform.rotation, noon.SunRotation), 0.01f);
            Assert.AreSame(noon.profile, volume.sharedProfile);
            Assert.AreEqual(0f, Shader.GetGlobalFloat(CampusWorldLighting.BakedShadowDropProperty), 1e-5f);
        }

        [Test]
        public void Lighting_WritesSceneHazeOnlyForTheActiveScene()
        {
            TimeOfDayLook noon = Look(TimeOfDay.Noon, 225f, 42f, 1f);
            TimeOfDayLook sunset = Look(TimeOfDay.Sunset, 250f, 6f, 0f);
            sunset.hazeDensity = 0.0042f;
            CampusWorldLighting lighting = World(noon, sunset, out Light sun, out Volume volume);
            Assert.AreEqual(SceneManager.GetActiveScene(), world.scene, "test world lives in the active scene");

            float before = RenderSettings.fogDensity;
            try
            {
                lighting.ShowInEditor(TimeOfDay.Sunset);
                Assert.AreEqual(0.0042f, RenderSettings.fogDensity, 1e-6f);
                Assert.AreSame(sun, RenderSettings.sun);
            }
            finally
            {
                RenderSettings.fogDensity = before;
            }
        }

        [Test]
        public void Lighting_WithoutLooks_KeepsItsOwnSun()
        {
            CampusWorldLighting lighting = World(null, null, out Light sun, out Volume volume);
            Quaternion before = sun.transform.rotation;
            lighting.ShowInEditor(TimeOfDay.Sunset);
            Assert.AreEqual(before, sun.transform.rotation, "no look for the mood = nothing to switch");
            Assert.IsNull(lighting.LookFor(TimeOfDay.Sunset));
        }

        private T Make<T>() where T : ScriptableObject
        {
            T o = ScriptableObject.CreateInstance<T>();
            made.Add(o);
            return o;
        }

        private TimeOfDayLook Look(TimeOfDay mood, float azimuth, float elevation, float bakedKeep)
        {
            TimeOfDayLook look = Make<TimeOfDayLook>();
            look.mood = mood;
            look.sunAzimuth = azimuth;
            look.sunElevation = elevation;
            look.sunIntensity = mood == TimeOfDay.Sunset ? 1.7f : 2.1f;
            look.bakedShadowKeep = bakedKeep;
            look.profile = Make<VolumeProfile>();
            return look;
        }

        // A tiny campus world: root with CampusWorldLighting, a Sun child and a look volume child, wired like the
        // builder does it (through the serialized fields).
        private CampusWorldLighting World(TimeOfDayLook noon, TimeOfDayLook sunset, out Light sun, out Volume volume)
        {
            world = new GameObject("TestCampusWorld");
            world.SetActive(false);                                  // wire everything before OnEnable runs
            var sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(world.transform, false);
            sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            var lookObject = new GameObject("Look");
            lookObject.transform.SetParent(world.transform, false);
            volume = lookObject.AddComponent<Volume>();
            CampusWorldLighting lighting = world.AddComponent<CampusWorldLighting>();
            var so = new UnityEditor.SerializedObject(lighting);
            so.FindProperty("sun").objectReferenceValue = sun;
            so.FindProperty("lookVolume").objectReferenceValue = volume;
            SerializedLooks(so, noon, sunset);
            so.ApplyModifiedPropertiesWithoutUndo();
            world.SetActive(true);
            return lighting;
        }

        private static void SerializedLooks(UnityEditor.SerializedObject so, TimeOfDayLook noon, TimeOfDayLook sunset)
        {
            UnityEditor.SerializedProperty list = so.FindProperty("looks");
            if (noon == null)
            {
                list.arraySize = 0;
                return;
            }
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = noon;
            list.GetArrayElementAtIndex(1).objectReferenceValue = sunset;
        }
    }
}
