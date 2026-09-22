import "@typespec/http";
import "@azure-tools/typespec-client-generator-core";
import "@azure-tools/typespec-azure-core";

using TypeSpec.Http;
using Azure.ClientGenerator.Core;

@service
@server("https://paging.example.test", "Paging")
namespace PagingService;

model Item { id: string; }

model Items {
  @pageItems
  @encodedName("application/json", "page.items")
  @encodedName("application/xml", "entries")
  values: Item[];
}

model NavigationPage extends Items {
  @nextLink @encodedName("application/json", "next.link") next?: url;
  @prevLink previous?: url;
  @firstLink first?: url;
  @lastLink last?: url;
  @continuationToken @encodedName("application/json", "out.cursor") cursor?: string;
}

@list @get @route("/all")
op all(
  @offset @query("$skip") skip?: int32,
  @pageIndex @query("page") index?: int32,
  @pageSize @query("$top") size?: int32,
  @continuationToken @query("in.cursor") incoming?: string,
): NavigationPage;

@get @route("/basic")
op basic(
  @offset @query("$skip") skip?: int32,
  @pageIndex @query("page") index?: int32,
  @pageSize @query("$top") size?: int32,
  @continuationToken @query("in.cursor") incoming?: string,
): NavigationPage;

@list @get @route("/offset")
op byOffset(@offset @query offset?: int32, @pageSize @query size?: int32): Items;

@list @get @route("/index")
op byIndex(@pageIndex @query index?: int32): Items;

@list @get @route("/items")
op itemsOnly(): Items;

model HeaderPage {
  @body payload: Items;
  @nextLink @header("x-next-url") next?: url;
  @continuationToken @header("x-next-token") outgoing?: string;
}

@list @get @route("/headers")
op headers(@continuationToken @query("cursor") incoming?: string): HeaderPage;

@get @route("/headers-basic")
op headersBasic(@continuationToken @query("cursor") incoming?: string): HeaderPage;

@list @get @route("/header-input")
op headerInput(@continuationToken @header("x-cursor") incoming?: string): HeaderPage;

@get @route("/path-input/{cursor}")
op pathInput(@continuationToken @path("cursor") incoming: string): HeaderPage;

@get @route("/cookie-input")
op cookieInput(@continuationToken @cookie("cursor") incoming?: string): HeaderPage;

model CursorOptions {
  @pageSize @encodedName("application/json", "page.size") size?: int32;
  @continuationToken @encodedName("application/json", "in.cursor") cursor?: string | null;
}

model InputOptions { @encodedName("application/json", "page.options") paging: CursorOptions; }
model InputBody { @encodedName("application/json", "all.options") options: InputOptions; }

@list @post @route("/nested")
op nested(@body payload: InputBody): HeaderPage;

@post @route("/request-media")
op requestMedia(
  @header contentType: "application/json" | "application/xml",
  @body payload: InputBody,
): Items;

@get @route("/query-object")
op queryObject(@query(#{ name: "options", explode: true }) options: CursorOptions): Items;

model ReinjectedOptions { @query("include-extra") includeExtra?: string; }
model ReinjectedPage extends Items {
  @nextLink next?: Azure.Core.Legacy.parameterizedNextLink<[ReinjectedOptions.includeExtra]>;
}

@list @get @route("/reinjected")
op reinjected(...ReinjectedOptions): ReinjectedPage;

@get @route("/reinjected-basic")
op reinjectedBasic(...ReinjectedOptions): ReinjectedPage;

scalar CustomNextLink extends Azure.Core.Legacy.parameterizedNextLink<[ReinjectedOptions.includeExtra]>;
model NullableLinkPage extends Items { @nextLink next?: CustomNextLink | null; }

@get @route("/reinjected-nullable")
op reinjectedNullable(...ReinjectedOptions): NullableLinkPage;

model SharedCursor { @continuationToken cursor?: string; }

@post @route("/shared-cursor")
op sharedCursor(
  @query(#{ explode: true }) options: SharedCursor,
  @body payload: SharedCursor,
): Items;

model SharedCursorPage extends Items { token: SharedCursor; }
model SharedCursorResponse {
  @header("x-options") options: SharedCursor;
  @body payload: SharedCursorPage;
}

@get @route("/shared-response")
op sharedResponse(): SharedCursorResponse;

@discriminated
union PageChoice { paged: Items, other: Item }

@list @get @route("/union-body")
op unionBody(): { @body payload: PageChoice; };

model SharedHeaders { @continuationToken @header("x-token") token?: string; }
model JsonPage extends SharedHeaders {
  @statusCode status: 200;
  @header contentType: "application/json";
  @body payload: Items;
}
model XmlPage extends SharedHeaders {
  @statusCode status: 201;
  @header contentType: "application/xml";
  @body differentWrapper: Items;
}

@get @route("/alternatives")
op alternatives(): JsonPage | XmlPage;

@list @get @route("/whole-body")
op wholeBody(): { @pageItems @body payload: Item[]; };

@get @route("/body-token")
op bodyToken(@continuationToken @body token: string): Items;

@get @route("/output-token")
op outputToken(): HeaderPage;

@Legacy.disablePageable
@list @get @route("/disabled")
op disabled(): NavigationPage;

@Legacy.markAsPageable
@get @route("/forced")
op forced(@pageSize @query limit?: int32): { value: Item[]; };

@Legacy.nextLinkVerb("POST")
@get @route("/post-next")
op postNext(): NavigationPage;

model PageStatus extends Azure.Core.Foundations.OperationStatus<Items> {}

@get @route("/page-status/{id}")
op getPageStatus(@path id: string): PageStatus;

@list @Azure.Core.pollingOperation(getPageStatus)
@post @route("/start-pages")
op startPages(): {
  @statusCode status: 202;
  @header("operation-location") pollingUrl: string;
  @body payload: Items;
};

@Azure.Core.pollingOperation(getPageStatus)
@post @route("/start-pages-basic")
op startPagesBasic(): {
  @statusCode status: 202;
  @header("operation-location") pollingUrl: string;
  @body payload: Items;
};

@get @route("/ordinary")
op ordinary(): Item;

/**
 * A shared parameter alias carrying a paging marker, spread onto an operation whose
 * body is opaque. The client deserializes the payload itself, so there is no property
 * `@pageItems` could be placed on.
 */
alias SharedMarker = {
  @continuationToken
  @query
  marker?: string;
};

@get @route("/opaque")
op opaqueWithMarker(...SharedMarker): {
  @body body: bytes;
};
