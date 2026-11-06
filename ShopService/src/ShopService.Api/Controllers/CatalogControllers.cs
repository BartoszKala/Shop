using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopService.Api.Extensions;
using ShopService.Application.Contracts;
using ShopService.Application.Services;
using ShopService.Domain;

namespace ShopService.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/categories")]
[Produces("application/json")]
public sealed class CategoriesController(CategoryService categoryService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoryResponse>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<CategoryResponse>> GetAll(CancellationToken ct) =>
        await categoryService.GetAllAsync(ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> Get(CategoryId id, CancellationToken ct) =>
        this.ToActionResult(await categoryService.GetAsync(id, ct));
}

[ApiController]
[AllowAnonymous]
[Route("api/products")]
[Produces("application/json")]
public sealed class ProductsController(ProductService productService) : ControllerBase
{
    /// <summary>Lista aktywnych produktów – filtr po kategorii, wyszukiwanie po nazwie i opisie, sortowanie, stronicowanie.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<ProductListItem>>(StatusCodes.Status200OK)]
    public async Task<PagedResponse<ProductListItem>> Search([FromQuery] ProductSearchRequest request, CancellationToken ct) =>
        await productService.SearchAsync(request, includeInactive: false, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> Get(ProductId id, CancellationToken ct) =>
        this.ToActionResult(await productService.GetAsync(id, includeInactive: false, ct));
}
