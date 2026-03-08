using System;
using NUnit.Framework;
using AshenThrone.Core;

namespace AshenThrone.Tests.Core
{
    [TestFixture]
    public class ServiceLocatorTests
    {
        // Dummy service types for testing
        private class FooService { public int Value; }
        private class BarService { public string Name; }

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Shutdown();
            ServiceLocator.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Shutdown();
        }

        // -------------------------------------------------------------------
        // Register + Get
        // -------------------------------------------------------------------

        [Test]
        public void Register_Get_ReturnsSameInstance()
        {
            var foo = new FooService { Value = 42 };
            ServiceLocator.Register<FooService>(foo);
            var result = ServiceLocator.Get<FooService>();
            Assert.AreSame(foo, result);
        }

        [Test]
        public void Register_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ServiceLocator.Register<FooService>(null));
        }

        [Test]
        public void Register_Overwrite_ReturnsNewInstance()
        {
            var foo1 = new FooService { Value = 1 };
            var foo2 = new FooService { Value = 2 };
            ServiceLocator.Register<FooService>(foo1);
            ServiceLocator.Register<FooService>(foo2);
            Assert.AreSame(foo2, ServiceLocator.Get<FooService>());
        }

        [Test]
        public void Get_NotRegistered_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => ServiceLocator.Get<FooService>());
        }

        // -------------------------------------------------------------------
        // TryGet
        // -------------------------------------------------------------------

        [Test]
        public void TryGet_Registered_ReturnsTrue()
        {
            ServiceLocator.Register<FooService>(new FooService());
            bool found = ServiceLocator.TryGet<FooService>(out var service);
            Assert.IsTrue(found);
            Assert.IsNotNull(service);
        }

        [Test]
        public void TryGet_NotRegistered_ReturnsFalse()
        {
            bool found = ServiceLocator.TryGet<FooService>(out var service);
            Assert.IsFalse(found);
            Assert.IsNull(service);
        }

        // -------------------------------------------------------------------
        // IsRegistered
        // -------------------------------------------------------------------

        [Test]
        public void IsRegistered_True_WhenRegistered()
        {
            ServiceLocator.Register<FooService>(new FooService());
            Assert.IsTrue(ServiceLocator.IsRegistered<FooService>());
        }

        [Test]
        public void IsRegistered_False_WhenNotRegistered()
        {
            Assert.IsFalse(ServiceLocator.IsRegistered<FooService>());
        }

        // -------------------------------------------------------------------
        // Unregister
        // -------------------------------------------------------------------

        [Test]
        public void Unregister_RemovesService()
        {
            ServiceLocator.Register<FooService>(new FooService());
            ServiceLocator.Unregister<FooService>();
            Assert.IsFalse(ServiceLocator.IsRegistered<FooService>());
        }

        [Test]
        public void Unregister_NotRegistered_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ServiceLocator.Unregister<FooService>());
        }

        // -------------------------------------------------------------------
        // Multiple service types
        // -------------------------------------------------------------------

        [Test]
        public void MultipleTypes_IndependentRegistration()
        {
            var foo = new FooService { Value = 10 };
            var bar = new BarService { Name = "test" };
            ServiceLocator.Register<FooService>(foo);
            ServiceLocator.Register<BarService>(bar);

            Assert.AreSame(foo, ServiceLocator.Get<FooService>());
            Assert.AreSame(bar, ServiceLocator.Get<BarService>());
        }

        [Test]
        public void Unregister_OneType_DoesNotAffectOther()
        {
            ServiceLocator.Register<FooService>(new FooService());
            ServiceLocator.Register<BarService>(new BarService());
            ServiceLocator.Unregister<FooService>();

            Assert.IsFalse(ServiceLocator.IsRegistered<FooService>());
            Assert.IsTrue(ServiceLocator.IsRegistered<BarService>());
        }

        // -------------------------------------------------------------------
        // Shutdown + Initialize
        // -------------------------------------------------------------------

        [Test]
        public void Shutdown_ClearsAllServices()
        {
            ServiceLocator.Register<FooService>(new FooService());
            ServiceLocator.Shutdown();
            ServiceLocator.Initialize();
            Assert.IsFalse(ServiceLocator.IsRegistered<FooService>());
        }

        [Test]
        public void Initialize_DoubleCall_IsIdempotent()
        {
            ServiceLocator.Register<FooService>(new FooService());
            ServiceLocator.Initialize(); // second call should not clear
            Assert.IsTrue(ServiceLocator.IsRegistered<FooService>());
        }
    }
}
