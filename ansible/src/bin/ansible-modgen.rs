use std::str::FromStr;

use az_ansible::modgen;
use az_core::uri::CrsUri;

fn main() -> anyhow::Result<()> {
    let m = modgen::ActionModule::new(CrsUri::from_str("crs://azure/config/set")?);

    tokio::runtime::Builder::new_current_thread()
        .enable_all()
        .build()?
        .block_on(async {
            let doc = m.generate_module().await.unwrap();
            eprintln!("{}", doc);
        });

    anyhow::Ok(())
}
