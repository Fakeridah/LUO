using NUnit.Framework;
using UnityEngine;

namespace RidahFPS.Controllers.Tests
{
    public sealed class GokzMovementRulesTests
    {
        [Test]
        public void KzTimerConstantsMatchOfficialMode()
        {
            GokzModeSettings settings = GokzModeSettings.For(GokzMode.KzTimer);

            Assert.That(settings.maxSpeed, Is.EqualTo(250f).Within(0.0001f));
            Assert.That(settings.preVelModMax, Is.EqualTo(1.104f).Within(0.0001f));
            Assert.That(settings.perfSpeedCap, Is.EqualTo(380f).Within(0.0001f));
            Assert.That(settings.airMaxWishSpeed, Is.EqualTo(30f).Within(0.0001f));
            Assert.That(settings.airAccelerate, Is.EqualTo(100f).Within(0.0001f));
            Assert.That(settings.gravity, Is.EqualTo(800f).Within(0.0001f));
            Assert.That(settings.jumpImpulse, Is.EqualTo(301.993377f).Within(0.0001f));
            Assert.That(settings.maxVelocity, Is.EqualTo(2000f).Within(0.0001f));
        }

        [Test]
        public void PrestrafeMaximumIs276Hu()
        {
            float speed = GokzMovementRules.SpeedNormalHu * GokzMovementRules.PreVelModMax;
            Assert.That(speed, Is.EqualTo(276f).Within(0.0001f));
        }

        [Test]
        public void KzTimerPrestrafeChangesPlayerMaxWishSpeed()
        {
            float maxWishSpeedHu = GokzMovementRules.GetPlayerMaxSpeed(
                GokzMovementRules.SpeedNormalHu,
                GokzMovementRules.PreVelModMax);

            Assert.That(maxWishSpeedHu, Is.EqualTo(276f).Within(0.0001f));
        }

        [Test]
        public void PerfCapIs380Hu()
        {
            Assert.That(GokzMovementRules.ApplyPerfSpeedCap(410f), Is.EqualTo(380f).Within(0.0001f));
            Assert.That(GokzMovementRules.ApplyPerfSpeedCap(379.99f), Is.EqualTo(379.99f).Within(0.0001f));
        }

        [Test]
        public void AirAccelerateUses30HuAddSpeedCap()
        {
            Vector3 result = GokzMovementRules.AirAccelerate(
                Vector3.zero,
                Vector3.forward,
                250f / GokzMovementRules.HammerUnitsPerUnityUnit,
                100f,
                30f / GokzMovementRules.HammerUnitsPerUnityUnit,
                1f);

            Assert.That(
                result.magnitude * GokzMovementRules.HammerUnitsPerUnityUnit,
                Is.EqualTo(30f).Within(0.0001f));
        }

        [Test]
        public void GroundAccelerationUsesWishSpeedAndSurfaceFriction()
        {
            Vector3 result = GokzMovementRules.Accelerate(
                Vector3.zero,
                Vector3.forward,
                250f / GokzMovementRules.HammerUnitsPerUnityUnit,
                6.5f,
                1f);

            float expectedHu = 6.5f * (1f / 128f) * 250f;
            Assert.That(
                result.magnitude * GokzMovementRules.HammerUnitsPerUnityUnit,
                Is.EqualTo(expectedHu).Within(0.0001f));
        }

        [Test]
        public void FrictionUsesStopSpeedAndDoesNotReverse()
        {
            Vector3 result = GokzMovementRules.ApplyFriction(
                Vector3.forward * (10f / GokzMovementRules.HammerUnitsPerUnityUnit),
                80f / GokzMovementRules.HammerUnitsPerUnityUnit,
                5f,
                1f);

            Assert.That(result.magnitude, Is.GreaterThanOrEqualTo(0f));
            Assert.That(result.magnitude, Is.LessThan(10f / GokzMovementRules.HammerUnitsPerUnityUnit));
        }

        [Test]
        public void WaterFrictionIsSpeedProportional()
        {
            Vector3 result = GokzMovementRules.ApplyWaterFriction(
                Vector3.forward * (250f / GokzMovementRules.HammerUnitsPerUnityUnit),
                5f,
                1f);

            float expected = 250f - 250f * 5f / 128f;
            Assert.That(
                result.magnitude * GokzMovementRules.HammerUnitsPerUnityUnit,
                Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void AirAccelerationKeepsOriginalWishSpeedInAccelerationTerm()
        {
            Vector3 result = GokzMovementRules.AirAccelerate(
                Vector3.zero,
                Vector3.forward,
                250f / GokzMovementRules.HammerUnitsPerUnityUnit,
                1f,
                30f / GokzMovementRules.HammerUnitsPerUnityUnit,
                1f);

            float expected = 250f / 128f;
            Assert.That(
                result.magnitude * GokzMovementRules.HammerUnitsPerUnityUnit,
                Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void FullyDuckedCommandUsesTheCSGo85HuSpeedCrop()
        {
            float fullyDuckedMax = GokzMovementRules.SpeedNormalHu * 0.34f;
            Assert.That(fullyDuckedMax, Is.EqualTo(85f).Within(0.0001f));
        }

        [Test]
        public void CsGoGroundAccelerationUsesFixedDuckScale()
        {
            Vector3 result = GokzMovementRules.AccelerateCsGo(
                Vector3.zero,
                Vector3.forward,
                250f / GokzMovementRules.HammerUnitsPerUnityUnit,
                6.5f,
                1f,
                0.34f);

            float expectedHu = 6.5f * (1f / 128f) * 250f * 0.34f;
            Assert.That(
                result.magnitude * GokzMovementRules.HammerUnitsPerUnityUnit,
                Is.EqualTo(expectedHu).Within(0.0001f));
        }
    }
}
