using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Sqids.AspNetCore.Tests;

public sealed class SqidAspNetCoreTests
{
	[Test]
	public async Task RouteConstraint_MatchesOnlyCanonicalValues()
	{
		var constraint = new SqidRouteConstraint();
		var values = new RouteValueDictionary();

		await Assert.That(constraint.Match(null, null, "id", values, RouteDirection.IncomingRequest)).IsFalse();

		values["id"] = null;
		await Assert.That(constraint.Match(null, null, "id", values, RouteDirection.IncomingRequest)).IsFalse();

		values["id"] = Sqid.From(1);
		await Assert.That(constraint.Match(null, null, "id", values, RouteDirection.IncomingRequest)).IsTrue();

		values["id"] = "Uk";
		await Assert.That(constraint.Match(null, null, "id", values, RouteDirection.IncomingRequest)).IsTrue();

		values["id"] = "UkX";
		await Assert.That(constraint.Match(null, null, "id", values, RouteDirection.IncomingRequest)).IsFalse();

		values["id"] = 123456789012345L;
		await Assert.That(constraint.Match(null, null, "id", values, RouteDirection.IncomingRequest)).IsFalse();

		values["id"] = new StringlyValue();
		await Assert.That(constraint.Match(null, null, "id", values, RouteDirection.IncomingRequest)).IsTrue();
	}

	[Test]
	public async Task BinderAndProvider_RejectNullArguments()
	{
		var binder = SqidModelBinder.Instance;
		await Assert.That(() => { _ = binder.BindModelAsync(null!); }).Throws<ArgumentNullException>();

		var provider = new SqidModelBinderProvider();
		await Assert.That(() => { _ = provider.GetBinder(null!); }).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task Binder_BindsNullableWhitespaceToNull()
	{
		var context = CreateContext(typeof(Sqid?), " ");
		await SqidModelBinder.Instance.BindModelAsync(context);

		await Assert.That(context.Result.IsModelSet).IsTrue();
		await Assert.That(context.Result.Model).IsNull();
	}

	[Test]
	public async Task Binder_RejectsWhitespaceForNonNullable()
	{
		var context = CreateContext(typeof(Sqid), "");
		await SqidModelBinder.Instance.BindModelAsync(context);

		await Assert.That(context.Result.IsModelSet).IsFalse();
		await Assert.That(context.ModelState.ErrorCount).IsEqualTo(1);
	}

	[Test]
	public async Task Binder_BindsValidAndRejectsInvalid()
	{
		var valid = CreateContext(typeof(Sqid), "Uk");
		await SqidModelBinder.Instance.BindModelAsync(valid);
		await Assert.That(valid.Result.IsModelSet).IsTrue();
		await Assert.That((Sqid)valid.Result.Model!).IsEqualTo(Sqid.From(1));

		var invalid = CreateContext(typeof(Sqid), "UkX");
		await SqidModelBinder.Instance.BindModelAsync(invalid);
		await Assert.That(invalid.Result.IsModelSet).IsFalse();
		await Assert.That(invalid.ModelState.ErrorCount).IsEqualTo(1);
	}

	[Test]
	public async Task AddSqids_RegistersServices()
	{
		var services = new ServiceCollection();
		services.AddSqids();

		using var provider = services.BuildServiceProvider();
		var mvc = provider.GetRequiredService<IOptions<MvcOptions>>().Value;
		await Assert.That(mvc.ModelBinderProviders[0]).IsTypeOf<SqidModelBinderProvider>();

		var routes = provider.GetRequiredService<IOptions<RouteOptions>>().Value;
		await Assert.That(routes.ConstraintMap[SqidRouteConstraint.Name]).IsEqualTo(typeof(SqidRouteConstraint));

		await Assert.That(provider.GetRequiredService<SqidCodec>()).IsSameReferenceAs(Sqid.Codec);
	}

	[Test]
	public async Task MvcIntegration_BindsSqidFromRouteAndQuery()
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();
		builder.Services.AddControllers().AddApplicationPart(typeof(SqidTestController).Assembly);
		builder.Services.AddSqids();

		await using var app = builder.Build();
		app.MapControllers();
		await app.StartAsync();

		var client = app.GetTestClient();

		await Assert.That((await client.GetAsync("/bind/route/Uk")).StatusCode).IsEqualTo(HttpStatusCode.OK);
		await Assert.That(await client.GetStringAsync("/bind/route/Uk")).IsEqualTo("1");
		await Assert.That((await client.GetAsync("/bind/route/UkX")).StatusCode).IsEqualTo(HttpStatusCode.NotFound);

		await Assert.That(await client.GetStringAsync("/bind/query?id=Uk")).IsEqualTo("1");
		await Assert.That((await client.GetAsync("/bind/query?id=UkX")).StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

		await Assert.That(await client.GetStringAsync("/bind/optional")).IsEqualTo("none");
		await Assert.That((await client.GetAsync("/bind/empty?id=")).StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

		await Assert.That(await client.GetStringAsync("/bind/int/5")).IsEqualTo("5");
	}

	private static ModelBindingContext CreateContext(Type modelType, string text)
	{
		var metadataProvider = new EmptyModelMetadataProvider();
		return new DefaultModelBindingContext
		{
			ModelMetadata = metadataProvider.GetMetadataForType(modelType),
			ModelName = "id",
			ValueProvider = new StubValueProvider(text),
			ModelState = new ModelStateDictionary(),
		};
	}

	private sealed class StringlyValue
	{
		public override string ToString() => "Uk";
	}

	private sealed class StubValueProvider(string text) : IValueProvider
	{
		public bool ContainsPrefix(string prefix) => true;

		public ValueProviderResult GetValue(string key) => new(text);
	}
}
