using System;
using Loyalty.Core.Domain;
using Loyalty.Core.Domain.Coupons;
using Loyalty.Core.Domain.Loyalty;
using Loyalty.Core.Domain.Members;
using Xunit;

namespace Loyalty.UnitTests.Domain;

/// <summary>Prueba los invariantes del dominio sin depender de EF Core ni infraestructura.</summary>
public class MemberTests
{
    [Fact]
    public void AwardPoints_IncrementsBalance()
    {
        var m = Member.Create(Guid.NewGuid(), Guid.NewGuid(), "5691234567", "Ana Pérez", null, "qr1", "s1");
        m.AwardPoints(25);
        Assert.Equal(25, m.PointsBalance);
    }

    [Fact]
    public void RedeemPoints_WhenSufficient_Decrements()
    {
        var m = Member.Create(Guid.NewGuid(), Guid.NewGuid(), "5691234567", "Ana", null, "qr1", "s1");
        m.AwardPoints(100);
        m.RedeemPoints(40);
        Assert.Equal(60, m.PointsBalance);
    }

    [Fact]
    public void RedeemPoints_Insufficient_Throws()
    {
        var m = Member.Create(Guid.NewGuid(), Guid.NewGuid(), "5691234567", "Ana", null, "qr1", "s1");
        m.AwardPoints(10);
        Assert.Throws<DomainException>(() => m.RedeemPoints(50));
        // El saldo no se altera tras el fallo (invariante).
        Assert.Equal(10, m.PointsBalance);
    }
}

public class CouponTests
{
    [Fact]
    public void TryRedeem_FirstTime_ReturnsTrue_AndMarksUsed()
    {
        var c = Coupon.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Combo", null, "CODE-1", null, null, null);
        var ok = c.TryRedeem(Guid.NewGuid(), Guid.NewGuid());
        Assert.True(ok);
        Assert.Equal(CouponStatus.Used, c.Status);
    }

    [Fact]
    public void TryRedeem_Twice_ReturnsFalse_SecondTime_Atomic()
    {
        var c = Coupon.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Combo", null, "CODE-2", null, null, null);
        var cashier = Guid.NewGuid(); var store = Guid.NewGuid();
        Assert.True(c.TryRedeem(cashier, store));
        Assert.False(c.TryRedeem(cashier, store)); // doble escaneo en concurrencia
        Assert.Equal(CouponStatus.Used, c.Status);
    }

    [Fact]
    public void TryRedeem_Expired_ReturnsFalse_MarksExpired()
    {
        var c = Coupon.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Combo", null, "CODE-3",
            DateTimeOffset.UtcNow.AddMinutes(-1), null, null);
        Assert.False(c.TryRedeem(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(CouponStatus.Expired, c.Status);
    }
}

public class MemberStampTests
{
    [Fact]
    public void Add_AccumulatesPositiveQuantity()
    {
        var s = MemberStamp.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SKU-BURGER");
        s.Add(3);
        s.Add(2);
        Assert.Equal(5, s.Count);
    }

    [Fact]
    public void Add_NonPositive_Throws()
    {
        var s = MemberStamp.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SKU-BURGER");
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Add(0));
    }
}