fn main() {
    if std::env::var("CARGO_MANIFEST_BUILD_PROTO").is_err() {
        return;
    }

    // compile the module and command proto
    tonic_build::configure()
        .build_client(true)
        .build_server(false)
        .out_dir("src/pb")
        .compile_protos(
            &["../protos/module.proto", "../protos/command.proto"],
            &[".."],
        )
        .unwrap_or_else(|e| panic!("Failed to compile protos {:?}", e));

    tonic_build::configure()
        .build_client(false)
        .build_server(true)
        .out_dir("src/pb")
        .compile_protos(&["../protos/context.proto"], &[".."])
        .unwrap_or_else(|e| panic!("Failed to compile protos {:?}", e));
}
