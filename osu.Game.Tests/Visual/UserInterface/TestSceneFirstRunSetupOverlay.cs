// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.FirstRunSetup;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.UserInterface
{
    public class TestSceneFirstRunSetupOverlay : OsuManualInputManagerTestScene
    {
        private FirstRunSetupOverlay overlay;

        private readonly Mock<IPerformFromScreenRunner> performer = new Mock<IPerformFromScreenRunner>();

        private readonly Mock<INotificationOverlay> notificationOverlay = new Mock<INotificationOverlay>();

        private Notification lastNotification;

        protected OsuConfigManager LocalConfig;

        [BackgroundDependencyLoader]
        private void load()
        {
            Dependencies.Cache(LocalConfig = new OsuConfigManager(LocalStorage));
            Dependencies.CacheAs(performer.Object);
            Dependencies.CacheAs(notificationOverlay.Object);
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("setup dependencies", () =>
            {
                performer.Reset();
                notificationOverlay.Reset();

                performer.Setup(g => g.PerformFromScreen(It.IsAny<Action<IScreen>>(), It.IsAny<IEnumerable<Type>>()))
                         .Callback((Action<IScreen> action, IEnumerable<Type> types) => action(null));

                notificationOverlay.Setup(n => n.Post(It.IsAny<Notification>()))
                                   .Callback((Notification n) => lastNotification = n);
            });

            AddStep("add overlay", () =>
            {
                Child = overlay = new FirstRunSetupOverlay
                {
                    State = { Value = Visibility.Visible }
                };
            });
        }

        [Test]
        public void TestBasic()
        {
            AddAssert("overlay visible", () => overlay.State.Value == Visibility.Visible);
        }

        [Test]
        [Ignore("Enable when first run setup is being displayed on first run.")]
        public void TestDoesntOpenOnSecondRun()
        {
            AddStep("set first run", () => LocalConfig.SetValue(OsuSetting.ShowFirstRunSetup, true));

            AddUntilStep("step through", () =>
            {
                if (overlay.CurrentScreen?.IsLoaded != false) overlay.NextButton.TriggerClick();
                return overlay.State.Value == Visibility.Hidden;
            });

            AddAssert("first run false", () => !LocalConfig.Get<bool>(OsuSetting.ShowFirstRunSetup));

            AddStep("add overlay", () =>
            {
                Child = overlay = new FirstRunSetupOverlay();
            });

            AddWaitStep("wait some", 5);

            AddAssert("overlay didn't show", () => overlay.State.Value == Visibility.Hidden);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestOverlayRunsToFinish(bool keyboard)
        {
            AddUntilStep("step through", () =>
            {
                if (overlay.CurrentScreen?.IsLoaded != false)
                {
                    if (keyboard)
                        InputManager.Key(Key.Enter);
                    else
                        overlay.NextButton.TriggerClick();
                }

                return overlay.State.Value == Visibility.Hidden;
            });

            AddUntilStep("wait for screens removed", () => !overlay.ChildrenOfType<Screen>().Any());

            AddStep("no notifications", () => notificationOverlay.VerifyNoOtherCalls());

            AddStep("display again on demand", () => overlay.Show());

            AddUntilStep("back at start", () => overlay.CurrentScreen is ScreenWelcome);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestBackButton(bool keyboard)
        {
            AddAssert("back button disabled", () => !overlay.BackButton.Enabled.Value);

            AddUntilStep("step to last", () =>
            {
                var nextButton = overlay.NextButton;

                if (overlay.CurrentScreen?.IsLoaded != false)
                    nextButton.TriggerClick();

                return nextButton.Text == CommonStrings.Finish;
            });

            AddUntilStep("step back to start", () =>
            {
                if (overlay.CurrentScreen?.IsLoaded != false)
                {
                    if (keyboard)
                        InputManager.Key(Key.Escape);
                    else
                        overlay.BackButton.TriggerClick();
                }

                return overlay.CurrentScreen is ScreenWelcome;
            });

            AddAssert("back button disabled", () => !overlay.BackButton.Enabled.Value);

            if (keyboard)
            {
                AddStep("exit via keyboard", () => InputManager.Key(Key.Escape));
                AddAssert("overlay dismissed", () => overlay.State.Value == Visibility.Hidden);
            }
        }

        [Test]
        public void TestClickAwayToExit()
        {
            AddStep("click inside content", () =>
            {
                InputManager.MoveMouseTo(overlay.ScreenSpaceDrawQuad.Centre);
                InputManager.Click(MouseButton.Left);
            });

            AddAssert("overlay not dismissed", () => overlay.State.Value == Visibility.Visible);

            AddStep("click outside content", () =>
            {
                InputManager.MoveMouseTo(new Vector2(overlay.ScreenSpaceDrawQuad.TopLeft.X, overlay.ScreenSpaceDrawQuad.Centre.Y));
                InputManager.Click(MouseButton.Left);
            });

            AddAssert("overlay dismissed", () => overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestResumeViaNotification()
        {
            AddStep("step to next", () => overlay.NextButton.TriggerClick());

            AddAssert("is at known screen", () => overlay.CurrentScreen is ScreenBeatmaps);

            AddStep("hide", () => overlay.Hide());
            AddAssert("overlay hidden", () => overlay.State.Value == Visibility.Hidden);

            AddStep("notification arrived", () => notificationOverlay.Verify(n => n.Post(It.IsAny<Notification>()), Times.Once));

            AddStep("run notification action", () => lastNotification.Activated());

            AddAssert("overlay shown", () => overlay.State.Value == Visibility.Visible);
            AddAssert("is resumed", () => overlay.CurrentScreen is ScreenBeatmaps);
        }
    }
}
