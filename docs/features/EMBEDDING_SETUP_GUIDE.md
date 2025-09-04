# AISwarm Embedding Configuration Guide

AISwarm supports multiple embedding service options to fit your needs and budget.

## 🔧 **Option 1: Self-Hosted (Bring Your Own Azure)**

Perfect for enterprises with existing Azure credits or strict data governance requirements.

### Prerequisites
- Azure subscription with OpenAI service enabled
- Azure OpenAI resource deployed

### Setup Steps

#### 1. Create Azure OpenAI Resource
```bash
# Using Azure CLI
az cognitiveservices account create \
  --name "aiswarm-openai" \
  --resource-group "your-resource-group" \
  --location "eastus" \
  --kind "OpenAI" \
  --sku "S0"
```

#### 2. Deploy Embedding Model
```bash
# Deploy text-embedding-ada-002
az cognitiveservices account deployment create \
  --name "aiswarm-openai" \
  --resource-group "your-resource-group" \
  --deployment-name "text-embedding-ada-002" \
  --model-name "text-embedding-ada-002" \
  --model-version "2" \
  --model-format "OpenAI" \
  --scale-settings-scale-type "Standard"
```

#### 3. Get API Keys
```bash
# Get your endpoint and keys
az cognitiveservices account show \
  --name "aiswarm-openai" \
  --resource-group "your-resource-group" \
  --query "properties.endpoint"

az cognitiveservices account keys list \
  --name "aiswarm-openai" \
  --resource-group "your-resource-group"
```

#### 4. Configure AISwarm
Add to your `appsettings.json`:
```json
{
  "AISwarm": {
    "Embedding": {
      "ServiceType": "SelfHosted",
      "AzureOpenAI": {
        "Endpoint": "https://aiswarm-openai.openai.azure.com/",
        "ApiKey": "your-api-key-here",
        "EmbeddingModel": "text-embedding-ada-002",
        "EmbeddingDimensions": 1536
      },
      "VectorStorage": {
        "StorageType": "SQLite",
        "ConnectionString": "Data Source=aiswarm_vectors.db"
      }
    }
  }
}
```

### Cost Estimation
- **Embedding Generation**: ~$0.0001 per 1K tokens
- **Typical Workspace**: 100 files × 2KB = ~$0.02 initial cost
- **Daily Updates**: ~5 files = ~$0.001/day
- **Monthly Cost**: ~$0.03 + Azure resource costs

---

## ☁️ **Option 2: AISwarm SaaS (Turnkey Solution)**

Zero-setup embedding service with transparent pricing and built-in optimizations.

### Benefits
- ✅ **No Azure setup required**
- ✅ **Optimized embeddings** for code
- ✅ **Automatic rate limiting**
- ✅ **Usage analytics**
- ✅ **Multi-model support**

### Pricing Tiers

#### 🌱 **Starter - $5/month**
- 10,000 embeddings/month
- SQLite vector storage
- Community support
- Perfect for small teams

#### 🚀 **Professional - $25/month**
- 100,000 embeddings/month
- PostgreSQL vector storage
- Priority support
- Usage analytics
- Team collaboration features

#### 🏢 **Enterprise - $100/month**
- 1,000,000 embeddings/month
- Advanced vector storage
- SLA guarantee
- Custom integrations
- Dedicated support

### Setup Steps

#### 1. Get API Key
```bash
# Visit https://aiswarm.dev/dashboard
# Create account and get API key
```

#### 2. Configure AISwarm
```json
{
  "AISwarm": {
    "Embedding": {
      "ServiceType": "AISwarmSaaS",
      "AISwarmSaaS": {
        "ApiKey": "asws_your_api_key_here",
        "ServiceUrl": "https://embeddings.aiswarm.dev",
        "Tier": "Professional"
      },
      "VectorStorage": {
        "StorageType": "SQLite",
        "ConnectionString": "Data Source=aiswarm_vectors.db"
      }
    }
  }
}
```

#### 3. Start Using
```bash
# Run the setup wizard
aiswarm setup embeddings

# Index your workspace
aiswarm index workspace ./src
```

---

## 🔧 **Configuration Wizard**

Run the interactive setup wizard:

```bash
aiswarm setup embeddings
```

Example wizard flow:
```
🤖 AISwarm Embedding Setup

Choose your embedding service:
1. Self-Hosted (Azure OpenAI) - Full control, bring your own keys
2. AISwarm SaaS - Turnkey solution, managed service
3. Offline Mode - Use existing embeddings only

Your choice [1-3]: 2

✅ AISwarm SaaS selected

Enter your AISwarm API key (or press Enter to get one):
[Leave empty to open registration page]

🌐 Opening https://aiswarm.dev/register...

API Key: asws_abc123...

Choose your tier:
1. Starter ($5/month) - 10K embeddings
2. Professional ($25/month) - 100K embeddings  
3. Enterprise ($100/month) - 1M embeddings

Your choice [1-3]: 2

✅ Configuration saved to appsettings.json
✅ Ready to index your workspace!

Next steps:
  aiswarm index workspace ./src
  aiswarm generate code "add error handling to UserService"
```

---

## 🔄 **Migration Between Options**

### From Self-Hosted to SaaS
```bash
aiswarm migrate embeddings --from self-hosted --to saas
```

### From SaaS to Self-Hosted  
```bash
aiswarm migrate embeddings --from saas --to self-hosted
```

Your existing vector embeddings are preserved during migration!

---

## 📊 **Monitoring & Analytics**

### Self-Hosted Monitoring
```bash
# Check embedding usage
aiswarm stats embeddings

# Monitor costs
aiswarm costs azure --resource-group "your-rg"
```

### SaaS Dashboard
Visit https://aiswarm.dev/dashboard for:
- Real-time usage tracking
- Cost breakdown by project
- Performance analytics
- Team collaboration metrics

---

## 🛠️ **Advanced Configuration**

### Vector Storage Options

#### SQLite (Default)
```json
"VectorStorage": {
  "StorageType": "SQLite",
  "ConnectionString": "Data Source=aiswarm_vectors.db",
  "MaxVectors": 100000
}
```

#### PostgreSQL (Scale)
```json
"VectorStorage": {
  "StorageType": "PostgreSQL", 
  "ConnectionString": "Host=localhost;Database=aiswarm;Username=user;Password=pass",
  "MaxVectors": 1000000
}
```

### Performance Tuning
```json
"Embedding": {
  "BatchSize": 10,
  "RateLimitPerMinute": 60,
  "RetryAttempts": 3,
  "CacheResults": true
}
```

---

## 🚨 **Troubleshooting**

### Common Issues

#### "Azure OpenAI quota exceeded"
- Check your Azure OpenAI quota limits
- Consider upgrading to higher tier
- Switch to AISwarm SaaS temporarily

#### "Vector database connection failed"
- Verify connection string format
- Ensure SQLite file permissions
- For PostgreSQL, check network connectivity

#### "Embedding dimension mismatch"
- Ensure model consistency
- Rebuild vector database if needed
- Check configuration file

### Getting Help
- 📖 Documentation: https://docs.aiswarm.dev
- 💬 Community: https://discord.gg/aiswarm
- 📧 Support: support@aiswarm.dev
- 🐛 Issues: https://github.com/aiswarm/aiswarm/issues