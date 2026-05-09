<style>
  .murang-page {
    color: #1d211f;
    font-family: "Pretendard", "Noto Sans KR", "Segoe UI", sans-serif;
    line-height: 1.65;
  }

  .murang-page * {
    box-sizing: border-box;
  }

  .murang-page a {
    color: inherit;
  }

  .hero {
    min-height: 540px;
    margin: -24px -24px 36px;
    padding: 52px min(7vw, 72px);
    display: grid;
    align-items: end;
    background:
      linear-gradient(90deg, rgba(8, 10, 11, 0.92), rgba(8, 10, 11, 0.58) 54%, rgba(8, 10, 11, 0.22)),
      url("docs/assets/pages/murang-cover.png") center / cover no-repeat;
    border-radius: 0 0 8px 8px;
    color: #f6f7ef;
  }

  .eyebrow {
    margin: 0 0 12px;
    color: #d7ff45;
    font-size: 14px;
    font-weight: 800;
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .hero h1 {
    margin: 0;
    color: #ffffff;
    font-size: clamp(48px, 9vw, 112px);
    line-height: 0.94;
    letter-spacing: 0;
  }

  .hero .lead {
    max-width: 740px;
    margin: 22px 0 28px;
    color: #f3f4e9;
    font-size: clamp(20px, 2.2vw, 30px);
    font-weight: 650;
  }

  .hero-actions,
  .quick-links {
    display: flex;
    flex-wrap: wrap;
    gap: 12px;
  }

  .hero-actions a,
  .quick-links a {
    display: inline-flex;
    align-items: center;
    min-height: 44px;
    padding: 10px 16px;
    border: 1px solid rgba(215, 255, 69, 0.55);
    border-radius: 999px;
    background: rgba(215, 255, 69, 0.12);
    color: #faffda;
    font-weight: 800;
    text-decoration: none;
  }

  .section {
    margin: 54px 0;
  }

  .section h2 {
    margin: 0 0 14px;
    color: #121412;
    font-size: clamp(30px, 4vw, 48px);
    line-height: 1.12;
    letter-spacing: 0;
  }

  .section h3 {
    margin: 0 0 10px;
    color: #171917;
    font-size: 22px;
  }

  .section-intro {
    max-width: 820px;
    margin: 0 0 26px;
    color: #3f4742;
    font-size: 18px;
  }

  .summary-grid,
  .feature-grid,
  .team-grid {
    display: grid;
    gap: 18px;
  }

  .summary-grid {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .feature-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .team-grid {
    grid-template-columns: repeat(5, minmax(0, 1fr));
  }

  .panel,
  .feature,
  .team-card,
  .video-panel {
    border: 1px solid #dbe2d4;
    border-radius: 8px;
    background: #fbfcf4;
    box-shadow: 0 10px 26px rgba(22, 28, 20, 0.08);
  }

  .panel,
  .team-card,
  .video-panel {
    padding: 22px;
  }

  .panel strong {
    display: block;
    margin-bottom: 8px;
    color: #151815;
    font-size: 19px;
  }

  .panel p,
  .feature p,
  .team-card p,
  .video-panel p {
    margin: 0;
    color: #465049;
  }

  .image-row {
    display: grid;
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
    gap: 18px;
    align-items: stretch;
  }

  .image-row figure,
  .feature figure,
  .wide-figure {
    margin: 0;
  }

  .image-row img,
  .feature img,
  .wide-figure img {
    display: block;
    width: 100%;
    border-radius: 8px;
    border: 1px solid #d9dfd2;
    background: #eef1e8;
  }

  .image-row figcaption,
  .wide-figure figcaption {
    margin-top: 8px;
    color: #667069;
    font-size: 14px;
  }

  .feature {
    overflow: hidden;
  }

  .feature .feature-body {
    padding: 20px;
  }

  .strategy-band {
    padding: 34px;
    border-radius: 8px;
    background: #101312;
    color: #f5f7ef;
  }

  .strategy-band h2,
  .strategy-band h3 {
    color: #ffffff;
  }

  .strategy-band .section-intro,
  .strategy-band p {
    color: #d6ded5;
  }

  .strategy-list {
    display: grid;
    grid-template-columns: repeat(4, minmax(0, 1fr));
    gap: 14px;
    margin-top: 22px;
  }

  .strategy-list div {
    min-height: 142px;
    padding: 18px;
    border-left: 4px solid #d7ff45;
    background: rgba(255, 255, 255, 0.06);
    border-radius: 8px;
  }

  .milestone {
    display: grid;
    grid-template-columns: 180px minmax(0, 1fr);
    gap: 18px;
    padding: 18px 0;
    border-top: 1px solid #dbe2d4;
  }

  .milestone strong {
    color: #151815;
  }

  .team-card strong {
    display: block;
    color: #111411;
    font-size: 18px;
  }

  .team-card span {
    display: block;
    margin: 4px 0 12px;
    color: #71806d;
    font-size: 13px;
    font-weight: 700;
  }

  .quick-links {
    margin-top: 18px;
  }

  .quick-links a {
    border-color: #c7d61f;
    background: #edf7ac;
    color: #1d211f;
  }

  @media (max-width: 900px) {
    .hero {
      min-height: 460px;
      margin: -18px -18px 28px;
      padding: 38px 24px;
    }

    .summary-grid,
    .feature-grid,
    .image-row,
    .strategy-list,
    .team-grid {
      grid-template-columns: 1fr;
    }

    .milestone {
      grid-template-columns: 1fr;
      gap: 6px;
    }
  }
</style>

<div class="murang-page">
  <section class="hero">
    <div>
      <p class="eyebrow">Kookmin SW Capstone Design</p>
      <h1>MU:RANG</h1>
      <p class="lead">연습실이 없어도, 악기가 없어도, 함께할 사람이 멀리 있어도 연주가 계속되는 VR 악기 연주 플랫폼</p>
      <div class="hero-actions">
        <a href="#project">프로젝트 소개</a>
        <a href="#features">핵심 기능</a>
        <a href="#team">팀 소개</a>
      </div>
    </div>
  </section>

  <section id="project" class="section">
    <p class="eyebrow">Why MU:RANG</p>
    <h2>연주를 포기하게 만드는 것은 의지보다 환경입니다.</h2>
    <p class="section-intro">
      MU:RANG은 MUsic과 유랑(RANG)을 결합한 이름입니다. 악기 연주를 배우고 싶지만 공간, 소음, 악기 구매 비용, 합주 인원 부족, 악보와 박자의 난이도 때문에 지속하지 못하는 사람들에게
      "누구나 즉시 즐길 수 있는 VR 연주 경험"을 제공하는 것이 목표입니다.
    </p>

    <div class="summary-grid">
      <div class="panel">
        <strong>공간의 한계 해소</strong>
        <p>가상 스튜디오에서 피아노, 드럼 등 여러 악기를 선택하고 소음 부담 없이 연주 경험을 시작합니다.</p>
      </div>
      <div class="panel">
        <strong>학습 장벽 완화</strong>
        <p>리듬게임 방식의 노트 가이드와 판정 피드백으로 악보를 바로 읽기 어려운 사용자도 곡을 따라갈 수 있습니다.</p>
      </div>
      <div class="panel">
        <strong>합주의 단절 해소</strong>
        <p>멀티플레이 기반 원격 합주를 통해 혼자 연습하던 사용자가 다른 연주자와 같은 공간감을 공유합니다.</p>
      </div>
    </div>
  </section>

  <section class="section">
    <p class="eyebrow">Midterm Feedback → Final Direction</p>
    <h2>중간평가 피드백을 서비스 정의로 구체화했습니다.</h2>
    <p class="section-intro">
      중간평가에서는 "VR기기를 활용한 악기 연주는 흥미롭고 참신하지만, 시장성과 서비스 구체성, 단계별 협주의 난이도 정의가 필요하다"는 피드백을 받았습니다.
      최종 발표 방향은 이 지점을 정면으로 보완합니다.
    </p>

    <div class="milestone">
      <strong>타깃 구체화</strong>
      <p>메인 타깃은 악기를 배우고 싶지만 중단 경험이 있는 20대 초중반 사용자, 보조 타깃은 합주 경험은 있으나 시간·공간 제약을 느끼는 사용자로 정의했습니다.</p>
    </div>
    <div class="milestone">
      <strong>난이도 구체화</strong>
      <p>곡 선택 후 난이도를 고르고, 차트 파일을 기반으로 노트 생성·이동·판정·피드백이 이어지는 리듬게임 학습 흐름을 명확히 했습니다.</p>
    </div>
    <div class="milestone">
      <strong>시장 진입 구체화</strong>
      <p>마케팅 제안서는 "연주 유목민, 이제 정착할 시간"이라는 메시지 아래 인플루언서 시딩, 체험형 이벤트, SNS 확산 캠페인을 결합합니다.</p>
    </div>
  </section>

  <section id="features" class="section">
    <p class="eyebrow">Service Structure</p>
    <h2>가상 악기, 리듬게임, 원격 합주를 하나의 연주 경험으로 묶습니다.</h2>
    <p class="section-intro">
      사용자는 가상 스튜디오에서 악기를 선택하고, 곡과 난이도를 고른 뒤 리듬게임 방식으로 연주합니다. 플레이어가 선택하지 않은 트랙은 자동 반주로 발화되어 한 명이 연주해도 곡 전체가 완성됩니다.
    </p>

    <div class="feature-grid">
      <article class="feature">
        <figure><img src="docs/assets/pages/instrument-selection.png" alt="MU:RANG 가상 스튜디오 악기 선택 화면 예시"></figure>
        <div class="feature-body">
          <h3>다양한 악기 선택</h3>
          <p>스튜디오를 이동하며 악기를 선택하고, 선택한 악기에 맞는 입력·사운드·시각 피드백을 제공합니다.</p>
        </div>
      </article>
      <article class="feature">
        <figure><img src="docs/assets/pages/rhythm-play.png" alt="MU:RANG 리듬게임 기반 연주 화면 예시"></figure>
        <div class="feature-body">
          <h3>리듬게임 연주 가이드</h3>
          <p>곡과 난이도 선택 후 노트 가이드를 따라 연주하며 Perfect, Good, Miss 판정으로 박자 정확도를 확인합니다.</p>
        </div>
      </article>
      <article class="feature">
        <figure><img src="docs/assets/pages/ensemble-play.png" alt="MU:RANG 합주 및 리듬게임 합주 화면 예시"></figure>
        <div class="feature-body">
          <h3>멀티플레이 원격 합주</h3>
          <p>리듬게임 없이 자유롭게 합주하거나, 리듬게임 세션에서 여러 사용자가 각자의 파트를 맡아 협주할 수 있습니다.</p>
        </div>
      </article>
      <article class="feature">
        <figure><img src="docs/assets/pages/service-overview.png" alt="MU:RANG 서비스 개요 슬라이드"></figure>
        <div class="feature-body">
          <h3>MU:RANG 브랜드 경험</h3>
          <p>연주를 배우고 싶지만 떠돌던 사용자가 한 공간에 머물며 음악을 계속하게 만드는 경험을 지향합니다.</p>
        </div>
      </article>
    </div>
  </section>

  <section class="section strategy-band">
    <p class="eyebrow">Business & Marketing</p>
    <h2>사업성은 "체험 → 공유 → 정착"의 흐름으로 설계합니다.</h2>
    <p class="section-intro">
      최종 발표용 마케팅 제안서는 악기 연주 입문자, 음악 게임 사용자, VR 기반 개인 연습 사용자를 핵심 오디언스로 보고,
      MU:RANG을 "연주 시작의 가장 쉬운 진입점"으로 포지셔닝합니다.
    </p>
    <div class="strategy-list">
      <div>
        <h3>인지</h3>
        <p>연주·기타·VR 게임 크리에이터 협업으로 "이건 게임인가, 진짜 연주인가?"라는 호기심을 만듭니다.</p>
      </div>
      <div>
        <h3>경험</h3>
        <p>영화관·체험관형 VR 콘서트 이벤트로 혼자가 아닌 연주 경험을 직접 느끼게 합니다.</p>
      </div>
      <div>
        <h3>확산</h3>
        <p>포스트잇형 옥외 캠페인과 SNS 인증을 통해 "연주를 붙이다"라는 컨셉을 공유 행동으로 연결합니다.</p>
      </div>
      <div>
        <h3>목표</h3>
        <p>제안서 초안 기준 총 노출 500만 회, QR 유입 5만 건, VR 체험 1.5만 건, SNS 게시물 5천 건을 캠페인 목표로 검토합니다.</p>
      </div>
    </div>
  </section>

  <section class="section">
    <p class="eyebrow">Technology</p>
    <h2>핸드 트래킹과 사운드 파이프라인 위에 리듬게임 세션을 올립니다.</h2>
    <p class="section-intro">
      Unity 6000.3.10f1, URP, OpenXR 기반으로 VR 입력을 처리하고, 악기 입력 이벤트를 MIDI 이벤트로 표준화해 사운드 엔진으로 전달합니다.
      리듬게임은 텍스트 차트 파일을 곡의 단일 진실원으로 삼아 노트 생성, 판정, 자동 반주를 구성합니다.
    </p>
    <figure class="wide-figure">
      <img src="docs/assets/pages/system-architecture.png" alt="MU:RANG 시스템 아키텍처 다이어그램">
      <figcaption>시스템 흐름: OpenXR 입력, 핸드 레이어, 악기 물리, MIDI 매핑, 사운드 엔진, 리듬게임 판정, 네트워크 동기화</figcaption>
    </figure>
  </section>

  <section id="video" class="section">
    <p class="eyebrow">Demo Video</p>
    <h2>소개 영상</h2>
    <div class="video-panel">
      <p>
        최종 발표용 시연 영상은 제작 중입니다. 영상이 완성되면 이 영역에 YouTube 또는 발표 데모 링크를 연결하고,
        현재 페이지의 서비스 화면·핵심 기능 설명과 함께 최종 산출물을 소개할 예정입니다.
      </p>
    </div>
  </section>

  <section id="team" class="section">
    <p class="eyebrow">Team</p>
    <h2>팀명은 밴드로 하겠습니다. 근데 이제 클래식을 곁들인</h2>
    <p class="section-intro">캡스톤 팀은 기획, 개발, 사운드, 그래픽, 멀티플레이, 마케팅을 나누어 MU:RANG을 만들고 있습니다.</p>

    <div class="team-grid">
      <div class="team-card">
        <strong>박하늘</strong>
        <span>20211343</span>
        <p>팀장·PM, 프로젝트 기획, 사운드 로직, 리듬게임 로직·악보 제작, 서류 작성</p>
      </div>
      <div class="team-card">
        <strong>권상혁</strong>
        <span>20212958</span>
        <p>프로젝트 기획, 핸드 트래킹, 컴퓨터 그래픽스, 악기 물리 상호작용, GitHub 총괄 관리</p>
      </div>
      <div class="team-card">
        <strong>허가림</strong>
        <span>20223157</span>
        <p>프로젝트 기획, 멀티플레이 구현, DB 연결, 서류 작성</p>
      </div>
      <div class="team-card">
        <strong>강태찬</strong>
        <span>20251345</span>
        <p>악기·플레이어·스튜디오 에셋 제작, 앱 아이콘 제작, PPT 디자인</p>
      </div>
      <div class="team-card">
        <strong>홍희성</strong>
        <span>20190221</span>
        <p>MU:RANG SNS 운영, SNS 광고 집행, 인플루언서 협찬, 펀딩·와디즈 운영, PPT 디자인</p>
      </div>
    </div>
  </section>

  <section id="usage" class="section">
    <p class="eyebrow">How To Run</p>
    <h2>실행 방법</h2>
    <div class="summary-grid">
      <div class="panel">
        <strong>1. 프로젝트 열기</strong>
        <p>Unity Hub에서 Unity 6000.3.10f1 환경으로 저장소를 열고 패키지 임포트가 끝날 때까지 기다립니다.</p>
      </div>
      <div class="panel">
        <strong>2. 씬 실행</strong>
        <p>주요 씬은 <code>Assets/Scenes/SampleScene.unity</code>입니다. XR 장비 연결 후 에디터 또는 빌드에서 실행합니다.</p>
      </div>
      <div class="panel">
        <strong>3. 연주 체험</strong>
        <p>가상 스튜디오에서 악기를 선택하고 곡·난이도를 고른 뒤 리듬게임 또는 합주 모드를 플레이합니다.</p>
      </div>
    </div>
    <div class="quick-links">
      <a href="https://github.com/kookmin-sw/2026-capstone-24">GitHub Repository</a>
      <a href="docs/specs/">Spec System</a>
    </div>
  </section>
</div>
