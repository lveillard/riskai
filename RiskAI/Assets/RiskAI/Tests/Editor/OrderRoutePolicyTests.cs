using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    /// <summary>Route lines: queue mode, the confirmation flash, and the untravelled remainder.</summary>
    public sealed class OrderRoutePolicyTests
    {
        [Test]
        public void LinesHideWhenShiftIsUpAfterTheFlash()
        {
            Assert.That(OrderRoutes.RouteVisible(false, OrderRoutes.ConfirmSeconds), Is.False);
            Assert.That(OrderRoutes.ConfirmAlpha(OrderRoutes.ConfirmSeconds), Is.EqualTo(0f));
            Assert.That(OrderRoutes.ConfirmAlpha(OrderRoutes.ConfirmSeconds + 1f), Is.EqualTo(0f));
        }

        [Test]
        public void LinesStayWhileShiftOrEncolarIsOn()
        {
            Assert.That(OrderRoutes.RouteVisible(true, -1f), Is.True);
            Assert.That(OrderRoutes.RouteVisible(true, OrderRoutes.ConfirmSeconds + 5f), Is.True);
        }

        [Test]
        public void APlainOrderFlashesAndFades()
        {
            Assert.That(OrderRoutes.RouteVisible(false, 0f), Is.True);
            Assert.That(OrderRoutes.ConfirmAlpha(0f), Is.EqualTo(1f));
            Assert.That(OrderRoutes.ConfirmAlpha(OrderRoutes.ConfirmSeconds * .5f), Is.EqualTo(.5f).Within(1e-5f));
            Assert.That(OrderRoutes.ConfirmAlpha(OrderRoutes.ConfirmSeconds * .5f), Is.LessThan(OrderRoutes.ConfirmAlpha(0f)));
        }

        [Test]
        public void HalfwayAlongThreeCornersDrawsOnlyTheRemainder()
        {
            var corners = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(10f, 0f, 10f)
            };
            var halfway = new Vector3(5f, 0f, 0f);
            int count = OrderRoutes.TrimTravelled(corners, 3, halfway);
            Assert.That(count, Is.EqualTo(3));
            Assert.That(corners[0], Is.EqualTo(halfway));
            Assert.That(corners[1], Is.EqualTo(new Vector3(10f, 0f, 0f)));
            Assert.That(corners[2], Is.EqualTo(new Vector3(10f, 0f, 10f)));
        }
    }
}
