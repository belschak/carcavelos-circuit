using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (request Q16): Luc's item pictures replace the code-drawn HUD icons, and an item without a
    // picture still shows its drawn icon (the slot never stays empty).
    public class ItemIconTests
    {
        private ItemSettings settings;
        private Texture2D espresso;
        private Texture2D wave;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<ItemSettings>();
            espresso = new Texture2D(4, 4);
            wave = new Texture2D(4, 4);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(settings);
            Object.DestroyImmediate(espresso);
            Object.DestroyImmediate(wave);
        }

        // Like Role4SceneBuilder: the icon fields are private and set through SerializedObject.
        private void SetIcon(string field, Texture2D picture)
        {
            SerializedObject so = new SerializedObject(settings);
            so.FindProperty(field).objectReferenceValue = picture;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void IconFor_GivesEachItemItsOwnPicture()
        {
            SetIcon("espressoIcon", espresso);
            SetIcon("carcavelosWaveIcon", wave);
            Assert.AreSame(espresso, settings.IconFor(ItemKind.Espresso));
            Assert.AreSame(wave, settings.IconFor(ItemKind.CarcavelosWave));
        }

        [Test]
        public void IconFor_NoneUnknownOrNotSet_IsNull()
        {
            SetIcon("espressoIcon", espresso);
            Assert.IsNull(settings.IconFor(ItemKind.None));
            Assert.IsNull(settings.IconFor((ItemKind)99), "unknown item");
            Assert.IsNull(settings.IconFor(ItemKind.DeansList), "no picture set: the HUD draws it in code");
        }

        [Test]
        public void ItemWithPicture_ShowsIt_AndDrawsNothing()
        {
            ItemIcon icon = new ItemIcon();
            icon.SetPicture(ItemKind.Espresso, espresso);
            icon.Kind = ItemKind.Espresso;
            Assert.IsTrue(icon.HasPicture);
            Assert.AreSame(espresso, icon.style.backgroundImage.value.texture);
        }

        [Test]
        public void ItemWithoutPicture_KeepsTheDrawnIcon()
        {
            ItemIcon icon = new ItemIcon();
            icon.SetPicture(ItemKind.Espresso, espresso);
            icon.Kind = ItemKind.Espresso;
            icon.Kind = ItemKind.SeagullDelivery; // the slot rolls on to an item with no picture
            Assert.IsFalse(icon.HasPicture);
            Assert.AreEqual(StyleKeyword.None, icon.style.backgroundImage.keyword);
        }

        [Test]
        public void PictureSetWhileShown_AppearsAtOnce_AndNullGoesBackToDrawing()
        {
            ItemIcon icon = new ItemIcon();
            icon.Kind = ItemKind.CarcavelosWave;
            icon.SetPicture(ItemKind.CarcavelosWave, wave);
            Assert.AreSame(wave, icon.style.backgroundImage.value.texture);
            icon.SetPicture(ItemKind.CarcavelosWave, null);
            Assert.IsFalse(icon.HasPicture);
            Assert.AreEqual(StyleKeyword.None, icon.style.backgroundImage.keyword);
        }

        [Test]
        public void SetPicture_ForNoneOrUnknownItem_IsIgnored()
        {
            ItemIcon icon = new ItemIcon();
            icon.SetPicture(ItemKind.None, espresso);
            icon.SetPicture((ItemKind)99, espresso);
            icon.SetPicture((ItemKind)(-1), espresso);
            icon.Kind = ItemKind.None;
            Assert.IsFalse(icon.HasPicture, "the empty slot never shows a picture");
        }
    }
}
