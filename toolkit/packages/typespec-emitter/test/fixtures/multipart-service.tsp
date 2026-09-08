import "@typespec/http";

using TypeSpec.Http;

@service
@server("https://multipart.example.test", "Multipart service")
namespace MultipartService;

model Metadata { label: string; }

model JsonEnvelope {
  @header("x-part-code") code?: int32;
  @header("x-part-constant") fixed: "accepted";
  @header(#{ name: "x-part-values", explode: true }) values: string[];
  @header contentType: "application/json" | "application/vnd.example+json";
  @body value: Metadata;
}

model JsonFile extends File {
  contentType: "application/json";
}

model TextFile is File<"text/plain", string>;

model Form {
  @doc("Metadata attached to the upload.")
  @encodedName("application/json", "json-metadata")
  metadata: HttpPart<JsonEnvelope, #{ name: "meta-data" }>;
  files?: HttpPart<JsonFile, #{ name: "documents" }>[];
  raw: HttpPart<bytes>;
  values: HttpPart<string[]>;
  fixed: HttpPart<"fixed">;
  text: HttpPart<TextFile>;
}

@route("/upload") @post
op upload(@header("x-request-token") token: string, @multipartBody body: Form): void;

@route("/upload-alternatives") @post
op uploadAlternatives(
  @header contentType: "multipart/form-data" | "multipart/mixed",
  @multipartBody body: Form,
): void;

@route("/download") @get
op download(): { @header("x-response-token") token: string; @multipartBody body: Form; };

enum Mode { first, second }
model TupleBase { id: string; }
model TupleChild { mode: Mode; }
model TupleData extends TupleBase { children: TupleChild[]; }
model TupleEnvelope {
  @header("x-tuple-code") code?: int32;
  @header(#{ name: "x-tuple-values", explode: true }) values: string[];
  @body data: TupleData;
}

@route("/tuple") @post
op tuple(
  @header contentType: "multipart/mixed",
  @multipartBody body: [HttpPart<TupleEnvelope>, HttpPart<bytes>[], HttpPart<string, #{ name: "text" }>],
): void;

@route("/tuple-form") @post
op tupleForm(
  @multipartBody body: [HttpPart<bytes, #{ name: "data" }>, HttpPart<string, #{ name: "text" }>],
): void;

@route("/tuple-download") @get
op tupleDownload(): {
  @header contentType: "multipart/mixed";
  @multipartBody body: [HttpPart<{ first: string }>, HttpPart<{ second: int32 }>];
};

model ResponseOnlyBase { id: string; }
model ResponseOnlyChild { value: int32; }
model ResponseOnlyData extends ResponseOnlyBase {
  children: ResponseOnlyChild[];
  next?: ResponseOnlyData;
  ignored: never;
}
model ResponseOnlyForm { data: HttpPart<ResponseOnlyData>; }

@route("/response-only") @get
op responseOnly(): { @multipartBody body: ResponseOnlyForm; };

@discriminator("kind")
model ResponseShape { kind: string; label: string; }
model Circle extends ResponseShape { kind: "circle"; radius: int32; }
model ShapeForm { shape: HttpPart<ResponseShape>; }

@route("/polymorphic-response") @get
op polymorphicResponse(): { @multipartBody body: ShapeForm; };

model FormReply {
  @statusCode status: 200;
  @header("x-form") form: string;
  @multipartBody body: Form;
}
model JsonReply {
  @statusCode status: 200;
  @header("x-json") json: int32;
  @body body: Metadata;
}

@route("/alternatives") @get
op alternatives(): FormReply | JsonReply;

model ErrorEnvelope { @header("x-part-error") code: int32; @body detail: string; }
model ErrorForm { problem: HttpPart<ErrorEnvelope>; }
@error
model MultipartError {
  @statusCode status: 400;
  @header("x-error") code: string;
  @multipartBody body: ErrorForm;
}

@route("/failure") @get
op failure(): MultipartError;

@route("/json") @post
op json(@body body: Metadata): Metadata;
