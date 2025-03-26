use az_cli::run;

fn main() {
    let mut args = std::env::args();
    args.next()
        .expect("application executable name is required"); // ignore the application executable name
    run(args);
}
