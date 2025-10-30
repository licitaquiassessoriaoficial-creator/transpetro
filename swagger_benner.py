import requests
import json

URL = "https://seguro-pjur.transpetro.com.br/swagger/docs/V1"
TIMEOUT = 30

try:
    r = requests.get(URL, timeout=TIMEOUT)
    r.raise_for_status()
    swagger = r.json()
    with open("swagger.json", "w", encoding="utf-8") as f:
        json.dump(swagger, f, ensure_ascii=False, indent=2)
    print("swagger.json salvo.")
    # Procurar por ProcessoDocumentos_InserirDocumento
    found = None
    for k, v in swagger.items():
        if "ProcessoDocumentos_InserirDocumento" in str(k):
            found = v
            break
        if isinstance(v, dict):
            for kk, vv in v.items():
                if "ProcessoDocumentos_InserirDocumento" in str(kk):
                    found = vv
                    break
    if found:
        print("\nProcessoDocumentos_InserirDocumento:")
        print(json.dumps(found, indent=2, ensure_ascii=False))
    else:
        print("Chave 'ProcessoDocumentos_InserirDocumento' não encontrada.")
except Exception as e:
    print(f"Erro ao baixar/analisar Swagger: {e}")
