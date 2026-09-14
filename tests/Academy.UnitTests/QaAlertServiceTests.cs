using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;
namespace Academy.UnitTests;
public sealed class QaAlertServiceTests
{
    [Fact] public async Task Review_MarksAlertReviewedAndCapturesAudit()
    {
        var alert=new QaAlert{Id=Guid.NewGuid(),Status=QaAlertStatus.Open,ReviewVersion=0,TimestampUtc=DateTimeOffset.UtcNow,CreatedAtUtc=DateTimeOffset.UtcNow,UpdatedAtUtc=DateTimeOffset.UtcNow};
        var alerts=new Mock<IQaAlertRepository>(); alerts.Setup(x=>x.GetByIdAsync(alert.Id,It.IsAny<CancellationToken>())).ReturnsAsync(alert);
        var unit=new Mock<IUnitOfWork>(); var service=new QaAlertService(alerts.Object,unit.Object); Guid reviewer=Guid.NewGuid();
        QaAlertDto result=await service.ReviewAsync(alert.Id,reviewer,new ReviewQaAlertRequest{Decision="Reviewed",Note="Evidence checked.",ExpectedReviewVersion=0});
        Assert.Equal(QaAlertStatus.Reviewed,alert.Status); Assert.Equal(reviewer,alert.ReviewedByUserId); Assert.Equal("Evidence checked.",alert.ReviewNote); Assert.Equal(1,alert.ReviewVersion); Assert.Equal("Reviewed",result.Status);
    }
    [Fact] public async Task Review_RejectsStaleVersion()
    {
        var alert=new QaAlert{Id=Guid.NewGuid(),Status=QaAlertStatus.Open,ReviewVersion=2}; var alerts=new Mock<IQaAlertRepository>(); alerts.Setup(x=>x.GetByIdAsync(alert.Id,It.IsAny<CancellationToken>())).ReturnsAsync(alert); var service=new QaAlertService(alerts.Object,Mock.Of<IUnitOfWork>());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ReviewAsync(alert.Id,Guid.NewGuid(),new ReviewQaAlertRequest{Decision="Ignored",ExpectedReviewVersion=1}));
    }
    [Fact] public async Task Review_ReopenClearsMetadata()
    {
        var alert=new QaAlert{Id=Guid.NewGuid(),Status=QaAlertStatus.Reviewed,ReviewedByUserId=Guid.NewGuid(),ReviewedAtUtc=DateTimeOffset.UtcNow,ReviewNote="Reviewed",ReviewVersion=3}; var alerts=new Mock<IQaAlertRepository>(); alerts.Setup(x=>x.GetByIdAsync(alert.Id,It.IsAny<CancellationToken>())).ReturnsAsync(alert); var service=new QaAlertService(alerts.Object,Mock.Of<IUnitOfWork>());
        await service.ReviewAsync(alert.Id,Guid.NewGuid(),new ReviewQaAlertRequest{Decision="Open",ExpectedReviewVersion=3}); Assert.Equal(QaAlertStatus.Open,alert.Status); Assert.Null(alert.ReviewedByUserId); Assert.Null(alert.ReviewedAtUtc); Assert.Null(alert.ReviewNote); Assert.Equal(4,alert.ReviewVersion);
    }
}
