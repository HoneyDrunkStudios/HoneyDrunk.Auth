# Deployment

## ADR-0005/0006 Bootstrap

Auth deployables bootstrap from environment variables only:

| Setting | Source | Purpose |
|---|---|---|
| `AZURE_KEYVAULT_URI` | App Service app setting | Points to the per-node vault. |
| `AZURE_APPCONFIG_ENDPOINT` | App Service app setting | Points to shared Azure App Configuration. |
| `HONEYDRUNK_NODE_ID` | App Service app setting | Must be `honeydrunk-auth` so App Configuration uses the Auth label. |

Provision one Key Vault per environment using the ADR-0005 naming convention:

```text
kv-hd-auth-{env}
```

Enable Azure RBAC on the vault. Access policies are forbidden. Grant the Auth managed identity permissions to read secrets from the environment-specific vault, and configure Event Grid to deliver `Microsoft.KeyVault.SecretNewVersionCreated` events to:

```text
/internal/vault/invalidate
```

The walkthrough for portal provisioning and OIDC federation lives in `HoneyDrunk.Architecture`; keep this repo's deployment notes limited to Auth-specific names and settings.

## Configuration Split

Secrets live only in Key Vault and must be read through `ISecretStore`.

| Secret | Location |
|---|---|
| `Jwt--SigningKeys` | `kv-hd-auth-{env}` |
| `VaultInvalidationWebhookSecret` | `kv-hd-auth-{env}` |

Non-secret settings live in shared App Configuration under the `honeydrunk-auth` label:

| Key | Example |
|---|---|
| `Auth:Issuer` | `https://issuer.example.com` |
| `Auth:Audience` | `api://honeydrunk-auth` |
| `Auth:ClockSkewSeconds` | `300` |

Do not pin Key Vault secret versions. Rotation relies on the Event Grid invalidation webhook so the next `ISecretStore` read resolves the latest version.
