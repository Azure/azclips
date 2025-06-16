use rustc_version::version;

fn main() {
    if std::env::var("CARGO_MANIFEST_BUILD_PROTO").is_err() {
        return;
    }

    // compile the module and command proto
    tonic_build::configure()
        // .build_client(true)
        // .build_server(true)
        .out_dir("src/pb")
        .compile_protos(
            &[
                "../protos/argument.proto",
                "../protos/command.proto",
                "../protos/component.proto",
                "../protos/plugin/condition.proto",
                "../protos/plugin/http.proto",
                "../protos/plugin/model.proto",
                "../protos/plugin/operation.proto",
                "../protos/plugin/output.proto",
                "../protos/plugin/resource.proto",
                "../protos/plugin/schema.proto",
                "../protos/plugin/selector.proto",
            ],
            &[".."],
        )
        .unwrap_or_else(|e| panic!("Failed to compile protos {:?}", e));

    let version = match version() {
        Ok(version) => version.to_string(),
        Err(_) => "unknown".to_owned(),
    };

    println!("cargo:rustc-env=AZCORE_RUSTC_VERSION={version}");
}
