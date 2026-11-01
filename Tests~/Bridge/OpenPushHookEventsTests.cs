using System;
using System.Collections.Generic;
using NUnit.Framework;
using OpenPushSDK.Core;

namespace OpenPushSDK.Tests
{
    /// The Logged, Registered and RegistrationFailed events sit beside the assignable OnLog, OnRegistered and
    /// OnRegistrationFailed: several subscribers can listen, the property still works as it always has, and
    /// both fire, the property first.
    public class OpenPushHookEventsTests : FacadeTestBase
    {
        [Test]
        public void SeveralSubscribersHearEveryLogLineAfterTheProperty()
        {
            var heard = new List<string>();
            OpenPush.OnLog = line => heard.Add("property " + line);
            Action<string> first = line => heard.Add("first " + line);
            OpenPush.Logged += first;
            OpenPush.Logged += line => heard.Add("second " + line);
            InitializeAndPump();
            heard.Clear();

            Platform.CoreFake.RaiseLog(OpenPushLogLevel.Error, "✗ failed");
            Main.Pump();
            CollectionAssert.AreEqual(new[] { "property ✗ failed", "first ✗ failed", "second ✗ failed" }, heard);

            heard.Clear();
            OpenPush.Logged -= first;
            Platform.CoreFake.RaiseLog(OpenPushLogLevel.Error, "✗ again");
            Main.Pump();
            CollectionAssert.AreEqual(new[] { "property ✗ again", "second ✗ again" }, heard, "-= removes one subscriber");
        }

        [Test]
        public void LogLevelGatesTheEventLikeTheProperty()
        {
            var property = new List<string>();
            var subscriber = new List<string>();
            OpenPush.OnLog = property.Add;
            OpenPush.Logged += subscriber.Add;
            OpenPush.LogLevel = OpenPushLogLevel.Error;
            InitializeAndPump();
            Platform.CoreFake.RaiseLog(OpenPushLogLevel.Verbose, "• bookkeeping");
            Platform.CoreFake.RaiseLog(OpenPushLogLevel.Error, "✗ failed");
            Main.Pump();
            CollectionAssert.AreEqual(property, subscriber);
            CollectionAssert.DoesNotContain(subscriber, "• bookkeeping");
            CollectionAssert.Contains(subscriber, "✗ failed");
        }

        [Test]
        public void RegisteredFiresThePropertyThenEverySubscriber()
        {
            var order = new List<string>();
            OpenPush.OnRegistered = () => order.Add("property");
            OpenPush.Registered += () => order.Add("first");
            OpenPush.Registered += () => order.Add("second");
            InitializeAndPump();
            Platform.CoreFake.RaiseRegistered();
            Main.Pump();
            CollectionAssert.AreEqual(new[] { "property", "first", "second" }, order);
        }

        [Test]
        public void AThrowingHandlerStopsNobodyElse()
        {
            var order = new List<string>();
            var logged = new List<string>();
            OpenPush.OnLog = logged.Add;
            OpenPush.OnRegistered = () => throw new InvalidOperationException("host bug");
            OpenPush.Registered += () => throw new InvalidOperationException("subscriber bug");
            OpenPush.Registered += () => order.Add("still heard");
            InitializeAndPump();
            Platform.CoreFake.RaiseRegistered();
            Main.Pump();
            Main.Pump();
            CollectionAssert.AreEqual(new[] { "still heard" }, order);
            Assert.AreEqual(2, logged.FindAll(l => l.Contains("an OpenPush event handler threw")).Count, "each throw is logged");
        }

        [Test]
        public void RegistrationFailedFiresForTheNativeFailureAndForARefusedInitialize()
        {
            var property = new List<string>();
            var subscriber = new List<string>();
            OpenPush.OnRegistrationFailed = property.Add;
            OpenPush.RegistrationFailed += subscriber.Add;

            OpenPush.Initialize("  ", "pk");   // refused inline: empty app id
            CollectionAssert.AreEqual(property, subscriber);
            Assert.AreEqual(1, subscriber.Count);
            StringAssert.Contains("app id is empty", subscriber[0]);

            InitializeAndPump();
            Platform.CoreFake.RaiseRegistrationFailed("✗ 401");
            Main.Pump();
            CollectionAssert.AreEqual(new[] { subscriber[0], "✗ 401" }, subscriber);
            CollectionAssert.AreEqual(property, subscriber, "the property hears the same, first");
        }

        [Test]
        public void ThePropertiesStayAssignableAndTheLastAssignmentWins()
        {
            // Games assign these with '=': that must keep compiling and keep replacing the handler.
            var heard = new List<string>();
            OpenPush.OnRegistered = () => heard.Add("old");
            OpenPush.OnRegistered = () => heard.Add("new");
            OpenPush.OnLog = null;   // restores the no-op sink
            Assert.IsNotNull(OpenPush.OnLog);
            InitializeAndPump();
            Platform.CoreFake.RaiseRegistered();
            Main.Pump();
            CollectionAssert.AreEqual(new[] { "new" }, heard);
            Assert.IsTrue(typeof(OpenPush).GetProperty(nameof(OpenPush.OnRegistered)).CanWrite);
            Assert.IsTrue(typeof(OpenPush).GetProperty(nameof(OpenPush.OnRegistrationFailed)).CanWrite);
            Assert.IsTrue(typeof(OpenPush).GetProperty(nameof(OpenPush.OnLog)).CanWrite);
        }
    }
}
