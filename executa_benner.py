# executa_benner.py
# Script completo: faz login no Benner Transpetro e executa o agendamento.

import sys
import requests
from bs4 import BeautifulSoup
from urllib.parse import urljoin

BASE = "https://seguro-pjur.transpetro.com.br"
LOGIN_PATH = "/Login"
USERNAME = "44201088863"
PASSWORD = "Abc123!@"

session = requests.Session()

COMMON_HEADERS = {
    "User-Agent": (
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
        "AppleWebKit/537.36 (KHTML, like Gecko) "
        "Chrome/141.0.0.0 Safari/537.36"
    ),
    "Accept-Language": "pt-BR,pt;q=0.9,en-US;q=0.8,en;q=0.7",
}

def save_file(filename: str, content: bytes):
    with open(filename, "wb") as f:
        f.write(content)
    print(f"✅ Resposta salva em: {filename}")

def get_login_page():
    url = urljoin(BASE, LOGIN_PATH)
    headers = COMMON_HEADERS.copy()
    headers.update({
        "Accept": ("text/html,application/xhtml+xml,application/xml;"
                   "q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8"),
        "Referer": url,
        "Connection": "keep-alive",
    })
    print("🔹 Acessando página de login...")
    resp = session.get(url, headers=headers, timeout=30)
    print("Status GET login:", resp.status_code)
    return resp

def parse_hidden_fields(html_text: str):
    soup = BeautifulSoup(html_text, "html.parser")
    viewstate = soup.find("input", {"id": "__VIEWSTATE"})
    viewstategen = soup.find("input", {"id": "__VIEWSTATEGENERATOR"})
    eventvalidation = soup.find("input", {"id": "__EVENTVALIDATION"})
    return {
        "__VIEWSTATE": viewstate["value"] if viewstate and viewstate.has_attr("value") else "",
        "__VIEWSTATEGENERATOR": viewstategen["value"] if viewstategen and viewstategen.has_attr("value") else "",
        "__EVENTVALIDATION": eventvalidation["value"] if eventvalidation and eventvalidation.has_attr("value") else "",
    }

def post_login(hidden):
    url = urljoin(BASE, LOGIN_PATH)
    headers = COMMON_HEADERS.copy()
    headers.update({
        "Content-Type": "application/x-www-form-urlencoded",
        "Origin": BASE,
        "Referer": url,
        "Upgrade-Insecure-Requests": "1",
    })

    data = {
        "__EVENTTARGET": "wesLogin$loginWes$LoginButton",
        "__EVENTARGUMENT": "",
        "__VIEWSTATE": hidden.get("__VIEWSTATE", ""),
        "__VIEWSTATEGENERATOR": hidden.get("__VIEWSTATEGENERATOR", ""),
        "__EVENTVALIDATION": hidden.get("__EVENTVALIDATION", ""),
        "wesLogin$loginWes$UserName": USERNAME,
        "wesLogin$loginWes$Password": PASSWORD,
    }

    print("🔹 Fazendo login...")
    resp = session.post(url, headers=headers, data=data, timeout=30, allow_redirects=True)
    print("Status do login:", resp.status_code)
    return resp

def post_agendamento():
    url_post = (
        "https://seguro-pjur.transpetro.com.br/jur/a/Z_AGENDAMENTOS/"
        "Form.aspx?p=1&pst=1dbafcc0ee3518e92beba4acd43176a151b34ee37099dbf4e3c1df891acb30fd"
    )

    headers_post = COMMON_HEADERS.copy()
    headers_post.update({
        "Accept": "*/*",
        "Cache-Control": "no-cache",
        "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
        "Origin": BASE,
        "Referer": url_post,
        "X-MicrosoftAjax": "Delta=true",
        "X-Requested-With": "XMLHttpRequest",
    })

    data_post = {
        "ctl00$ctl05$mainScriptManager": "ctl00$ctl05$mainScriptManager|ctl00$Main$Z_AGENDAMENTOS_FORM",
        "ctl00$__token_PJUR_TR": "1EXtg/ov5BFzYKzg2nwLn1alkouBP005i7IY+CKeZh6gwonVcx9+a12TrRQ3cdkhHgT2CncG4UtDq1guitf/BFQuWwawMN7niEECI+2M08QgSoXFWKsG+b3SG2IY34e3",
        "ctl00$Main$Z_AGENDAMENTOS_FORM$SelectedTab": "",
        "ctl00$Main$Z_AGENDAMENTOS_FORM$ENCRYPTED_HANDLE_HiddenField": "OWHDqZGrfxc2AbYtDO4QXfsLhbeEEo7bdoJ1XBabiGM=",
        "ctl00$Main$Z_AGENDAMENTOS_FORM$HANDLE_HiddenField": "24",
        "ctl00$Main$Z_AGENDAMENTOS_FORM$PageControl$GERAL$GERAL$NOME": "Distribuição de pastas",
        "__EVENTTARGET": "ctl00$Main$Z_AGENDAMENTOS_FORM",
        "__EVENTARGUMENT": "CMD_EXECUTAR",
        "__VIEWSTATEGENERATOR": "D8D8BEAB",
        "__SCROLLPOSITIONX": "0",
        "__SCROLLPOSITIONY": "0",
        "__ASYNCPOST": "true",
    }

    print("\n🔹 Executando agendamento...")
    resp = session.post(url_post, headers=headers_post, data=data_post, timeout=60)
    print("Status agendamento:", resp.status_code)
    save_file("resposta_agendamento.html", resp.content)

def main():
    # 1. Login
    resp_get = get_login_page()
    if resp_get.status_code != 200:
        print("⚠️ Erro ao obter página de login.")
        sys.exit(1)

    hidden = parse_hidden_fields(resp_get.text)
    resp_post_login = post_login(hidden)
    save_file("resposta_login.html", resp_post_login.content)

    if resp_post_login.status_code == 200:
        print("✅ Login aparentemente OK (redirecionamento detectado).")
    else:
        print("⚠️ Login falhou — verifique captcha, bloqueio ou senha incorreta.")
        sys.exit(1)

    # 2. Executa o agendamento
    post_agendamento()

if __name__ == "__main__":
    main()
