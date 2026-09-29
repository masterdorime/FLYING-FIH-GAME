using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class InputAssetTests
    {
        const string Path = "Assets/Input/PlayerInputActions.inputactions";

        [Test]
        public void M1ActionsExistWithCorrectBindings()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Path);
            Assert.IsNotNull(asset, "inputactions asset missing");
            var move = asset.FindAction("Gameplay/Move");
            Assert.IsNotNull(move);
            Assert.AreEqual("Vector2", move.expectedControlType);
            Assert.IsTrue(move.bindings.Any(b => b.path.Contains("/w")), "WASD binding missing");
            Assert.IsTrue(move.bindings.Any(b => b.path.Contains("leftStick")), "gamepad stick missing");
            Assert.IsNotNull(asset.FindAction("Gameplay/Pause"));
        }

        [Test]
        public void TimingActionExistsWithBindings()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Path);
            var timing = asset.FindAction("Gameplay/TimingAction");
            Assert.IsNotNull(timing, "TimingAction missing");
            Assert.AreEqual(InputActionType.Button, timing.type);
            Assert.IsTrue(timing.bindings.Any(b => b.path.Contains("/space")), "space binding missing");
            Assert.IsTrue(timing.bindings.Any(b => b.path.Contains("buttonSouth")), "gamepad binding missing");
        }
    }
}
