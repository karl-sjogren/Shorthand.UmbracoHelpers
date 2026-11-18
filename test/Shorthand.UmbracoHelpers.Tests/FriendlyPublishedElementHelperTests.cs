using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
#if !UMBRACO_17
using Umbraco.Cms.Core.Models.Membership;
#endif

namespace Shorthand.UmbracoHelpers.Tests;

public class FriendlyPublishedElementHelperTests {
#if !UMBRACO_17
    [Fact]
    public void ElementNamesUseInjectedUserService() {
        var users = A.Fake<IUserService>();
        var creator = A.Fake<IProfile>();
        var writer = A.Fake<IProfile>();
        var element = A.Fake<IPublishedElement>();
        A.CallTo(() => element.CreatorId).Returns(1);
        A.CallTo(() => element.WriterId).Returns(2);
        A.CallTo(() => creator.Name).Returns("Creator");
        A.CallTo(() => writer.Name).Returns("Writer");
        A.CallTo(() => users.GetProfileById(1)).Returns(creator);
        A.CallTo(() => users.GetProfileById(2)).Returns(writer);
        var helper = new FriendlyPublishedElementHelper(A.Fake<IPublishedValueFallback>(), A.Fake<IVariationContextAccessor>(), users);

        helper.CreatorName(element).ShouldBe("Creator");
        helper.WriterName(element).ShouldBe("Writer");
    }
#endif

    [Fact]
    public void ElementValueForwardsCultureAndSegment() {
        // Arrange
        var fallback = A.Fake<IPublishedValueFallback>();
        var helper = new FriendlyPublishedElementHelper(fallback, A.Fake<IVariationContextAccessor>(), A.Fake<IUserService>());
        var element = A.Fake<IPublishedElement>();
        var property = A.Fake<IPublishedProperty>();

        A.CallTo(() => element.GetProperty("title")).Returns(property);
        A.CallTo(() => property.HasValue("sv-SE", "desktop")).Returns(true);
        A.CallTo(() => property.GetValue("sv-SE", "desktop")).Returns("Hej");

        // Act & assert
        helper.Value(element, "title", "sv-SE", "desktop").ShouldBe("Hej");

        // Assert
        A.CallTo(() => property.GetValue("sv-SE", "desktop")).MustHaveHappenedOnceExactly();
    }
}
